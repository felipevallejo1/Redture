namespace Redture.Core.Infrastructure;

/// <inheritdoc cref="IAppPaths" />
public sealed class AppPaths : IAppPaths
{
    /// <summary>
    /// Environment variable that overrides the data directory. Useful for a
    /// portable build (point it next to the executable) and for integration
    /// tests that must not touch the real user profile.
    /// </summary>
    public const string DataDirectoryOverrideVariable = "REDTURE_DATA_DIR";

    private const string ApplicationFolderName = "Redture";

    public AppPaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = Path.GetFullPath(dataDirectory);
        SettingsFilePath = Path.Combine(DataDirectory, "settings.json");
        LogDirectory = Path.Combine(DataDirectory, "logs");
        CleanShutdownSentinelPath = Path.Combine(DataDirectory, ".running");
    }

    public string DataDirectory { get; }

    public string SettingsFilePath { get; }

    public string LogDirectory { get; }

    public string CleanShutdownSentinelPath { get; }

    /// <summary>
    /// Builds the default paths: <c>%APPDATA%\Redture</c> on Windows,
    /// <c>~/.config/Redture</c> on Linux, <c>~/Library/Application Support/Redture</c>
    /// on macOS — <see cref="Environment.SpecialFolder.ApplicationData"/> already
    /// resolves to the right convention on each OS.
    /// </summary>
    public static AppPaths CreateDefault()
    {
        string? overridden = Environment.GetEnvironmentVariable(DataDirectoryOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return new AppPaths(overridden);
        }

        string roaming = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create);

        return new AppPaths(Path.Combine(roaming, ApplicationFolderName));
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
