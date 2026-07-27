using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Redture.Core.Brightness;
using Redture.Core.Settings;
using Redture.Platform.Abstractions.Overlay;
using Redture.Platform.Abstractions.SystemEvents;

namespace Redture.App.Services;

/// <summary>
/// The single component allowed to push display corrections to the OS.
/// </summary>
/// <remarks>
/// <para>
/// Centralising this is not tidiness, it is the flicker-prevention strategy
/// from <c>docs/architecture.md</c>: every value change is funnelled through
/// one place, applied on one thread, and written only when the resulting state
/// actually differs from what the OS already has.
/// </para>
/// <para>
/// It also owns the two reactions Redture must always have: rebuilding the
/// overlay when displays change, and honouring the panic hotkey.
/// </para>
/// </remarks>
public sealed class DisplayCoordinator : IDisposable
{
    /// <summary>
    /// Display change messages arrive in bursts — plugging in a monitor can
    /// produce half a dozen in a second. Rebuilding on each one is itself a
    /// source of visible flicker, so only the last one in a burst wins.
    /// </summary>
    private static readonly TimeSpan DisplayChangeDebounce = TimeSpan.FromMilliseconds(500);

    private readonly ISettingsStore _settingsStore;
    private readonly IOverlayController _overlay;
    private readonly ISystemEvents _systemEvents;
    private readonly ILogger<DisplayCoordinator> _logger;

    /// <summary>Debounce ticket; see <see cref="OnDisplaysChanged"/>.</summary>
    private int _refreshGeneration;

    private bool _started;
    private bool _disposed;

    public DisplayCoordinator(
        ISettingsStore settingsStore,
        IOverlayController overlay,
        ISystemEvents systemEvents,
        ILogger<DisplayCoordinator> logger)
    {
        _settingsStore = settingsStore;
        _overlay = overlay;
        _systemEvents = systemEvents;
        _logger = logger;
    }

    /// <summary>
    /// Raised after the coordinator changes the settings on its own — currently
    /// only the panic reset. The UI listens so its sliders follow, rather than
    /// showing a value the screen no longer reflects.
    /// </summary>
    public event EventHandler? StateResetExternally;

    /// <summary>
    /// The panic shortcut, or null when it could not be registered.
    /// </summary>
    public string? PanicHotkeyDescription => _systemEvents.PanicHotkeyDescription;

    /// <summary>
    /// Subscribes to OS notifications and applies the stored state. Must run on
    /// the UI thread: the overlay and the message window both create window
    /// handles, which belong to the thread that created them.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_started)
        {
            return;
        }

        _started = true;

        _systemEvents.DisplaysChanged += OnDisplaysChanged;
        _systemEvents.PanicRequested += OnPanicRequested;
        _systemEvents.Start();

        Apply();
    }

    /// <summary>
    /// Recomputes the target state from the settings and pushes it to the OS.
    /// Cheap and idempotent — callers do not need to know whether anything
    /// actually changed.
    /// </summary>
    public void Apply()
    {
        if (_disposed)
        {
            return;
        }

        AppSettings settings = _settingsStore.Current;

        // hardwareAvailable is false until stage 1.5 introduces DDC/CI and WMI
        // backlight control. Until then the mapper's software-only path drives
        // the whole slider — the same path a monitor that refuses DDC/CI will
        // permanently take.
        BrightnessPlan plan = BrightnessMapper.Map(
            settings.Brightness,
            settings.MaxOverlayOpacity,
            hardwareAvailable: false);

        double opacity = settings.EffectsEnabled ? plan.OverlayOpacity : 0d;
        _overlay.SetOpacity(opacity);
    }

    private void OnDisplaysChanged(object? sender, EventArgs e)
    {
        int generation = Interlocked.Increment(ref _refreshGeneration);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DisplayChangeDebounce).ConfigureAwait(false);

                if (Volatile.Read(ref _refreshGeneration) != generation)
                {
                    return; // A later change superseded this one.
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _logger.LogInformation("Rebuilding overlays after a display change.");
                    _overlay.Refresh();
                    Apply();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle a display change.");
            }
        });
    }

    /// <summary>
    /// Returns the screen to a neutral state. This is the guarantee that a user
    /// who dims to near-black can always get back — it must work even if the
    /// control panel is unreachable behind the overlay.
    /// </summary>
    private void OnPanicRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            AppSettings settings = _settingsStore.Current;
            settings.Brightness = AppSettings.MaxBrightness;
            settings.TemperatureKelvin = AppSettings.NeutralTemperatureKelvin;
            _settingsStore.RequestSave();

            Apply();
            StateResetExternally?.Invoke(this, EventArgs.Empty);

            _logger.LogInformation("Panic reset applied: brightness and colour temperature back to neutral.");
        });
    }

    /// <summary>
    /// Tears the corrections down. Must run on the UI thread, and before the
    /// process exits: a leftover overlay is not possible (windows die with the
    /// process) but doing it explicitly keeps the shutdown ordering honest for
    /// the gamma ramp, which does survive and lands in stage 2.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _systemEvents.DisplaysChanged -= OnDisplaysChanged;
        _systemEvents.PanicRequested -= OnPanicRequested;

        _overlay.Dispose();
        _systemEvents.Dispose();

        _logger.LogDebug("Display coordinator disposed.");
    }
}
