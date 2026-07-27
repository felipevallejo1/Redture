using System.Text.Json;
using Microsoft.Extensions.Logging;
using Redture.Core.Infrastructure;

namespace Redture.Core.Settings;

/// <inheritdoc cref="ISettingsStore" />
public sealed class JsonSettingsStore : ISettingsStore
{
    /// <summary>
    /// How long to wait after the last change before writing. Long enough to
    /// swallow a slider drag, short enough that a user who alt-F4s right after
    /// tweaking something still keeps their change (and <see cref="FlushAsync"/>
    /// covers the graceful-shutdown case anyway).
    /// </summary>
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(750);

    private readonly IAppPaths _paths;
    private readonly ILogger<JsonSettingsStore> _logger;

    /// <summary>Serialises concurrent writers so two saves never interleave.</summary>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Monotonic counter used to debounce: each <see cref="RequestSave"/> takes
    /// a ticket, and only the task still holding the latest ticket when its
    /// delay elapses performs the write.
    /// </summary>
    private int _saveGeneration;

    private volatile bool _savePending;

    public JsonSettingsStore(IAppPaths paths, ILogger<JsonSettingsStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public AppSettings Current { get; private set; } = new();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();

        string path = _paths.SettingsFilePath;
        if (!File.Exists(path))
        {
            _logger.LogInformation("No settings file at {Path}; starting from defaults.", path);
            Current = new AppSettings();
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            AppSettings? loaded;
            await using (FileStream stream = File.OpenRead(path))
            {
                loaded = await JsonSerializer
                    .DeserializeAsync(stream, SettingsJsonContext.Default.AppSettings, cancellationToken)
                    .ConfigureAwait(false);
            }

            // A file containing literal "null" parses fine but yields null.
            Current = loaded ?? new AppSettings();
            Current.Normalize();
            _logger.LogInformation("Settings loaded from {Path} (schema v{Version}).", path, Current.SchemaVersion);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Settings file at {Path} is not valid JSON; falling back to defaults.", path);
            QuarantineCorruptFile(path);
            Current = new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Disk trouble is not a reason to refuse to start: run on defaults
            // and let the next save attempt surface the problem again.
            _logger.LogError(ex, "Could not read settings from {Path}; continuing with defaults.", path);
            Current = new AppSettings();
        }
    }

    public void RequestSave()
    {
        _savePending = true;
        int generation = Interlocked.Increment(ref _saveGeneration);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounce).ConfigureAwait(false);

                // A newer request arrived while we waited: it owns the write.
                if (Volatile.Read(ref _saveGeneration) != generation)
                {
                    return;
                }

                await SaveAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Fire-and-forget: nobody is awaiting us, so swallowing here is
                // the only way to keep the exception from going unobserved.
                _logger.LogError(ex, "Debounced settings save failed.");
            }
        });
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _paths.EnsureCreated();

            AppSettings snapshot = Current.Clone();
            snapshot.Normalize();
            snapshot.SchemaVersion = AppSettings.CurrentSchemaVersion;

            string targetPath = _paths.SettingsFilePath;
            string tempPath = targetPath + ".tmp";

            // Write-then-replace: a crash mid-write can only ever damage the
            // temporary file, never the settings the app will read next launch.
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, snapshot, SettingsJsonContext.Default.AppSettings, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            ReplaceAtomically(tempPath, targetPath);
            _savePending = false;
            _logger.LogDebug("Settings saved to {Path}.", targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not write settings to {Path}.", _paths.SettingsFilePath);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default) =>
        _savePending ? SaveAsync(cancellationToken) : Task.CompletedTask;

    /// <summary>
    /// Moves the temporary file over the real one as a single filesystem
    /// operation where the platform supports it.
    /// </summary>
    private static void ReplaceAtomically(string tempPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            try
            {
                File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                return;
            }
            catch (IOException)
            {
                // File.Replace requires both paths on the same volume and is not
                // supported by every filesystem; fall back to a plain move.
            }
            catch (PlatformNotSupportedException)
            {
            }
        }

        File.Move(tempPath, targetPath, overwrite: true);
    }

    /// <summary>
    /// Renames an unreadable settings file out of the way instead of deleting
    /// it, so the user can still recover hand-written values from it.
    /// </summary>
    private void QuarantineCorruptFile(string path)
    {
        try
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string backupPath = Path.Combine(
                Path.GetDirectoryName(path)!,
                $"settings.corrupt-{stamp}.json");

            File.Move(path, backupPath, overwrite: true);
            _logger.LogInformation("Corrupt settings file moved to {BackupPath}.", backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not quarantine the corrupt settings file at {Path}.", path);
        }
    }
}
