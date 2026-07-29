using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Redture.Core.Brightness;
using Redture.Core.Color;
using Redture.Core.Infrastructure;
using Redture.Core.Settings;
using Redture.Platform.Abstractions.Brightness;
using Redture.Platform.Abstractions.Gamma;
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
/// It also owns the reactions Redture must always have: rebuilding the overlay
/// when displays change, honouring the panic hotkey, and handing the backlight
/// back to the user when corrections are switched off.
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
    private readonly IHardwareBrightnessController _hardware;
    private readonly IGammaController _gamma;
    private readonly ColorConflictMonitor _conflicts;
    private readonly ISystemEvents _systemEvents;
    private readonly IFullscreenDetector _fullscreen;
    private readonly CleanShutdownSentinel _sentinel;
    private readonly ILogger<DisplayCoordinator> _logger;

    /// <summary>Debounce ticket; see <see cref="OnDisplaysChanged"/>.</summary>
    private int _refreshGeneration;

    /// <summary>
    /// True once the backlight has been handed back to the user because
    /// corrections are switched off. Prevents re-sending the restore on every
    /// subsequent call — each one is a blocking round trip to the monitor.
    /// </summary>
    private bool _backlightReleased;

    /// <summary>
    /// Temperature the schedule is asking for, or null when automation is off
    /// and the manual setting is in charge. Held here rather than written into
    /// the settings: it changes every few seconds, and overwriting the stored
    /// value would lose whatever the user chose by hand.
    /// </summary>
    private int? _scheduledTemperature;

    private bool _started;
    private bool _disposed;

    public DisplayCoordinator(
        ISettingsStore settingsStore,
        IOverlayController overlay,
        IHardwareBrightnessController hardware,
        IGammaController gamma,
        ColorConflictMonitor conflicts,
        ISystemEvents systemEvents,
        IFullscreenDetector fullscreen,
        CleanShutdownSentinel sentinel,
        ILogger<DisplayCoordinator> logger)
    {
        _fullscreen = fullscreen;
        _settingsStore = settingsStore;
        _overlay = overlay;
        _hardware = hardware;
        _gamma = gamma;
        _conflicts = conflicts;
        _systemEvents = systemEvents;
        _sentinel = sentinel;
        _logger = logger;
    }

    /// <summary>
    /// Raised after the coordinator changes state on its own — the panic reset,
    /// or the backlight probe finishing. The UI listens so its controls follow,
    /// rather than showing a value the screen no longer reflects.
    /// </summary>
    public event EventHandler? ExternalStateChanged;

    /// <summary>The panic shortcut, or null when it could not be registered.</summary>
    public string? PanicHotkeyDescription => _systemEvents.PanicHotkeyDescription;

    /// <summary>Displays whose backlight Redture can drive.</summary>
    public IReadOnlyList<HardwareBrightnessTarget> BacklightTargets => _hardware.Targets;

    /// <summary>Brightness value at which the backlight hands over to the overlay.</summary>
    public double BacklightSplitPoint => BrightnessMapper.DefaultHardwareSplitPoint;

    /// <summary>Whether any display accepted a colour lookup table.</summary>
    public bool ColorTemperatureSupported => _gamma.IsSupported;

    /// <summary>
    /// True when the OS refused the ramp — on Windows, usually its restriction
    /// on how far a ramp may deviate from linear.
    /// </summary>
    public bool ColorTemperatureRejected => _gamma.LastRampRejected;

    /// <summary>Displays where HDR makes the gamma ramp a no-op.</summary>
    public IReadOnlyList<string> DisplaysIgnoringColorTemperature => _gamma.DisplaysIgnoringGamma;

    /// <summary>Temperature actually on screen, whoever chose it.</summary>
    public int EffectiveTemperatureKelvin =>
        _scheduledTemperature ?? _settingsStore.Current.TemperatureKelvin;

    /// <summary>
    /// Hands the schedule's current target to the display, or null to give the
    /// manual setting control back.
    /// </summary>
    /// <remarks>
    /// Called from the automation loop's own thread. Marshalled to the UI
    /// thread because applying it touches the overlay windows, which belong to
    /// the thread that created them.
    /// </remarks>
    public void SetScheduledTemperature(int? kelvin)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || _scheduledTemperature == kelvin)
            {
                return;
            }

            _scheduledTemperature = kelvin;
            Apply();
        });
    }

    /// <summary>Whether an application currently owns the whole screen.</summary>
    public bool IsFullscreenActive => _fullscreen.IsFullscreenActive;

    /// <summary>Whether another application has been seen overwriting the LUT.</summary>
    public bool HasColorConflict => _conflicts.HasConflict;

    /// <summary>Names of the applications involved, where any were recognised.</summary>
    public IReadOnlyList<string> ConflictingApplications => _conflicts.Applications;

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
        _systemEvents.SessionResumed += OnSessionResumed;
        _systemEvents.PanicRequested += OnPanicRequested;
        _systemEvents.Start();

        _conflicts.ConflictDetected += (_, _) => ExternalStateChanged?.Invoke(this, EventArgs.Empty);

        _fullscreen.FullscreenStateChanged += OnFullscreenStateChanged;
        _fullscreen.Start();

        // A gamma ramp survives the process that set it. If the previous run
        // was killed, the display may still be carrying its tint with nothing
        // left to explain it, so clear the slate before applying anything.
        if (_sentinel.PreviousRunWasUnclean)
        {
            _logger.LogInformation("Previous run ended abnormally; forcing displays back to a linear ramp first.");
            _gamma.ResetToLinear();
        }

        Apply();

        // Backlight discovery is deliberately not on this path: probing DDC/CI
        // costs roughly 60 ms per monitor and would delay the tray icon by that
        // much for no reason.
        _ = Task.Run(ProbeBacklightAsync);
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

        if (!settings.EffectsEnabled)
        {
            _overlay.SetOpacity(0d);
            _gamma.Apply(GammaRamp.Linear);
            _conflicts.SetTintApplied(false);

            if (!_backlightReleased)
            {
                _hardware.RestoreInitial();
                _backlightReleased = true;
            }

            return;
        }

        _backlightReleased = false;

        int temperature = EffectiveTemperatureKelvin;
        _gamma.Apply(GammaRampBuilder.Build(temperature));

        // Only worth watching for a conflict while Redture is actually asking
        // for a tint: with a neutral ramp there is nothing to be overwritten.
        _conflicts.SetTintApplied(temperature != AppSettings.NeutralTemperatureKelvin);

        // An application owning the screen sits below nothing: the overlay would
        // not be visible over exclusive fullscreen, and competing for z-order
        // with a game produces exactly the flicker this design avoids
        // everywhere else. The gamma ramp above is still applied — it costs
        // nothing, and some fullscreen applications leave it alone.
        if (settings.SuspendOverlayInFullscreen && _fullscreen.IsFullscreenActive)
        {
            _overlay.SetOpacity(0d);
            return;
        }

        BrightnessPlan plan = BrightnessMapper.Map(
            settings.Brightness,
            settings.MaxOverlayOpacity,
            _hardware.IsAvailable);

        _overlay.SetOpacity(plan.OverlayOpacity);

        if (plan.HardwareBrightness is { } backlight)
        {
            _hardware.SetBrightness(backlight);
        }
    }

    /// <summary>
    /// Probes for backlight control off the UI thread, then adopts what it
    /// finds and re-applies.
    /// </summary>
    private async Task ProbeBacklightAsync()
    {
        try
        {
            _hardware.Refresh();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed)
                {
                    return;
                }

                AdoptBacklightLevelOnFirstRun();
                Apply();
                ExternalStateChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backlight discovery failed; the whole brightness range stays in software.");
        }
    }

    /// <summary>
    /// On the first run that finds backlight control, moves the slider to
    /// wherever the display already is instead of overwriting it.
    /// </summary>
    /// <remarks>
    /// Without this, installing Redture would push a monitor deliberately set
    /// to 20% up to full brightness, purely because 100 is the default value of
    /// a setting nobody has touched. It runs once and never again — after that
    /// the slider is the source of truth.
    /// </remarks>
    private void AdoptBacklightLevelOnFirstRun()
    {
        AppSettings settings = _settingsStore.Current;

        if (settings.HardwareBrightnessAdopted || !_hardware.IsAvailable)
        {
            return;
        }

        if (_hardware.CurrentPercent is not { } currentPercent)
        {
            return;
        }

        double adopted = BrightnessMapper.Unmap(
            currentPercent,
            overlayOpacity: 0d,
            settings.MaxOverlayOpacity,
            hardwareAvailable: true);

        settings.Brightness = adopted;
        settings.HardwareBrightnessAdopted = true;
        _settingsStore.RequestSave();

        _logger.LogInformation(
            "Adopted the display's own backlight level ({Current:0}%) as a brightness of {Adopted:0}.",
            currentPercent,
            adopted);
    }

    private void OnDisplaysChanged(object? sender, EventArgs e)
    {
        int generation = Interlocked.Increment(ref _refreshGeneration);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DisplayChangeDebounce).ConfigureAwait(false);

                if (Volatile.Read(ref _refreshGeneration) != generation || _disposed)
                {
                    return; // A later change superseded this one.
                }

                _logger.LogInformation("Rebuilding after a display change.");

                // Backlight handles do not survive a topology change, and
                // re-probing is slow, so it happens here rather than on the UI
                // thread.
                _hardware.Refresh();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _overlay.Refresh();
                    Apply();
                    ExternalStateChanged?.Invoke(this, EventArgs.Empty);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle a display change.");
            }
        });
    }

    /// <summary>
    /// Repairs the colour correction after Windows discarded it.
    /// </summary>
    /// <remarks>
    /// A lock screen, a UAC prompt or a user switch resets the LUT without
    /// telling anyone. The controller is told to forget what it believes the
    /// driver holds, because that belief is exactly what is now wrong.
    /// </remarks>
    private void OnSessionResumed(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            _logger.LogInformation("Session resumed; re-applying colour correction.");
            _gamma.Refresh();
        });
    }

    /// <summary>
    /// Stands down while an application owns the screen, and puts everything
    /// back when it lets go.
    /// </summary>
    private void OnFullscreenStateChanged(object? sender, bool isFullscreen)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            Apply();

            // So the panel can say whether it is standing down right now, which
            // is the only way to tell this feature is working at all.
            ExternalStateChanged?.Invoke(this, EventArgs.Empty);

            if (!isFullscreen)
            {
                _ = ReassertGammaAsync();
            }
        });
    }

    /// <summary>
    /// Writes the colour ramp again after something else has had the display,
    /// and keeps checking briefly that it stuck.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single write on the way out is not enough. A fullscreen application
    /// hands the display back over some hundreds of milliseconds, and anything
    /// it does to the ramp during that window lands <em>after</em> the write —
    /// leaving the screen showing one thing while the slider says another, with
    /// nothing to correct it until the user happens to move a control.
    /// </para>
    /// <para>
    /// Bounded on purpose. Three attempts over about a second is enough to
    /// outlast a mode change and far too few to become a fight with a colour
    /// tool that genuinely wants the ramp; that case belongs to
    /// <see cref="ColorConflictMonitor"/>.
    /// </para>
    /// </remarks>
    private async Task ReassertGammaAsync()
    {
        const int Attempts = 3;

        try
        {
            for (int attempt = 1; attempt <= Attempts && !_disposed; attempt++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(350)).ConfigureAwait(false);

                if (_gamma.Verify() != GammaVerification.Foreign)
                {
                    return;
                }

                _logger.LogInformation(
                    "The colour ramp did not survive the return to the desktop; re-applying (attempt {Attempt} of {Total}).",
                    attempt,
                    Attempts);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!_disposed)
                    {
                        _gamma.Refresh();
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-apply the colour ramp after returning to the desktop.");
        }
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
            ExternalStateChanged?.Invoke(this, EventArgs.Empty);

            _logger.LogInformation("Panic reset applied: brightness and colour temperature back to neutral.");
        });
    }

    /// <summary>
    /// Tears the corrections down. Must run on the UI thread, and before the
    /// process exits: the overlay windows belong to this thread, and the
    /// backlight has to be handed back to the user.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _systemEvents.DisplaysChanged -= OnDisplaysChanged;
        _systemEvents.SessionResumed -= OnSessionResumed;
        _systemEvents.PanicRequested -= OnPanicRequested;
        _fullscreen.FullscreenStateChanged -= OnFullscreenStateChanged;
        _fullscreen.Dispose();

        _conflicts.Dispose();

        // Order matters. The gamma ramp is global driver state that outlives
        // this process, so it is the one thing that must be handed back first
        // and unconditionally.
        _gamma.Dispose();

        // Disposing the backlight controller restores every display to the
        // level it had before Redture started.
        _hardware.Dispose();
        _overlay.Dispose();
        _systemEvents.Dispose();

        _logger.LogDebug("Display coordinator disposed.");
    }
}
