using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Redture.App.Infrastructure;
using Redture.App.Services;
using Redture.Core.Infrastructure;
using Redture.Core.Scheduling;
using Redture.Core.Settings;
using Redture.Platform.Abstractions.Brightness;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Gamma;
using Redture.Platform.Abstractions.Startup;

namespace Redture.App.ViewModels;

/// <summary>
/// Backing view model for the control panel.
/// </summary>
/// <remarks>
/// Every change follows the same two steps: write it into the live
/// <see cref="AppSettings"/> and schedule a debounced save, then ask
/// <see cref="DisplayCoordinator"/> to push the new state to the screen. The
/// view model never touches an OS API itself — it does not even know whether
/// dimming is done by a backlight or by an overlay.
/// </remarks>
public sealed partial class ControlPanelViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly IDisplayEnumerator _displayEnumerator;
    private readonly DisplayCoordinator _coordinator;
    private readonly AutomationService _automation;
    private readonly IGammaRangeUnlock _gammaRange;
    private readonly IAutoStartService _autoStart;
    private readonly IAppPaths _paths;
    private readonly ILogger<ControlPanelViewModel> _logger;

    /// <summary>
    /// Set while the view model is being refreshed from the settings, so the
    /// generated change handlers do not write straight back to the source they
    /// are reading from.
    /// </summary>
    private bool _suppressPersist;

    [ObservableProperty]
    private bool _effectsEnabled;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private int _temperatureKelvin;

    [ObservableProperty]
    private bool _automationEnabled;

    [ObservableProperty]
    private bool _startWithSystem;

    [ObservableProperty]
    private int _dayTemperatureKelvin;

    [ObservableProperty]
    private int _nightTemperatureKelvin;

    [ObservableProperty]
    private int _transitionMinutes;

    [ObservableProperty]
    private bool _useSolarTimes;

    [ObservableProperty]
    private string _latitudeText = string.Empty;

    [ObservableProperty]
    private string _longitudeText = string.Empty;

    [ObservableProperty]
    private string _sunriseText = string.Empty;

    [ObservableProperty]
    private string _sunsetText = string.Empty;

    public ControlPanelViewModel(
        ISettingsStore settingsStore,
        IDisplayEnumerator displayEnumerator,
        DisplayCoordinator coordinator,
        AutomationService automation,
        IGammaRangeUnlock gammaRange,
        IAutoStartService autoStart,
        IAppPaths paths,
        ILogger<ControlPanelViewModel> logger)
    {
        _autoStart = autoStart;
        _settingsStore = settingsStore;
        _displayEnumerator = displayEnumerator;
        _coordinator = coordinator;
        _automation = automation;
        _gammaRange = gammaRange;
        _paths = paths;
        _logger = logger;

        // Seed the backing fields directly: assigning through the generated
        // properties would fire the change handlers and schedule a pointless
        // save of values we just read from disk.
        AppSettings settings = settingsStore.Current;
        _effectsEnabled = settings.EffectsEnabled;
        _brightness = settings.Brightness;
        _temperatureKelvin = settings.TemperatureKelvin;
        _automationEnabled = settings.AutomationEnabled;

        ScheduleSettings schedule = settings.Schedule;
        _dayTemperatureKelvin = schedule.DayTemperatureKelvin;
        _nightTemperatureKelvin = schedule.NightTemperatureKelvin;
        _transitionMinutes = schedule.TransitionMinutes;
        _useSolarTimes = schedule.UseSolarTimes;
        _latitudeText = schedule.Latitude?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;
        _longitudeText = schedule.Longitude?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;
        _sunriseText = schedule.ManualSunrise.ToString("HH\\:mm", CultureInfo.InvariantCulture);
        _sunsetText = schedule.ManualSunset.ToString("HH\\:mm", CultureInfo.InvariantCulture);

        // The registry is the source of truth for this, not the settings file:
        // the user can remove the entry from Task Manager's startup tab without
        // Redture ever hearing about it, and a switch showing the wrong state
        // would be worse than no switch.
        _startWithSystem = autoStart.IsEnabled;

        _automation.StateChanged += (_, _) => RefreshScheduleStatus();

        // The panic hotkey and the backlight probe both change state behind the
        // UI's back; without this the controls would keep showing something the
        // screen no longer reflects.
        _coordinator.ExternalStateChanged += (_, _) => ReloadFromCoordinator();
    }

    /// <summary>Displays currently attached, refreshed when the panel opens.</summary>
    public ObservableCollection<DisplayInfo> Displays { get; } = [];

    // --- Slider bounds, surfaced so the view never hardcodes a range ---------

    public double MinBrightness => AppSettings.MinBrightness;

    public double MaxBrightness => AppSettings.MaxBrightness;

    public int MinTemperatureKelvin => AppSettings.MinTemperatureKelvin;

    public int MaxTemperatureKelvin => AppSettings.MaxTemperatureKelvin;

    // --- Read-only informational text ---------------------------------------

    public string VersionLabel => $"Redture {LoggingSetup.Version}";

    public string SettingsPath => _paths.SettingsFilePath;

    public string LogPath => _paths.LogDirectory;

    /// <summary>
    /// Only advertises the escape hatch when it was actually registered — the
    /// combination may already belong to another application.
    /// </summary>
    public string PanicHotkeyHint => _coordinator.PanicHotkeyDescription is { } hotkey
        ? $"Press {hotkey} at any time to reset brightness and colour to neutral."
        : "The panic hotkey could not be registered; another app is likely using it.";

    /// <summary>
    /// Explains which mechanism is doing the dimming, and where the handover
    /// sits. Worth surfacing: the same slider position behaves differently on a
    /// monitor that accepts DDC/CI and one that does not.
    /// </summary>
    public string BacklightSummary
    {
        get
        {
            IReadOnlyList<HardwareBrightnessTarget> targets = _coordinator.BacklightTargets;

            if (targets.Count == 0)
            {
                return "No backlight control detected on this display, so the whole range is dimmed in software.";
            }

            string names = string.Join(
                ", ",
                targets.Select(target => $"{target.Name} ({DescribeMechanism(target.Mechanism)})"));

            return $"Backlight control: {names}. Above {_coordinator.BacklightSplitPoint:0}% the slider drives the real backlight; below it, the overlay takes over.";
        }
    }

    /// <summary>
    /// Explains what the colour temperature slider is actually doing — which
    /// matters here more than for brightness, because the gamma ramp has two
    /// ways of not working that look identical from the outside.
    /// </summary>
    public string TemperatureStatus
    {
        get
        {
            IReadOnlyList<string> hdrDisplays = _coordinator.DisplaysIgnoringColorTemperature;
            if (hdrDisplays.Count > 0)
            {
                return $"{string.Join(", ", hdrDisplays)} is in HDR mode. Windows ignores gamma ramps there, so colour temperature has no effect on it. Turning HDR off restores it.";
            }

            if (!_coordinator.ColorTemperatureSupported)
            {
                return "No display accepted a colour lookup table, so colour temperature cannot be applied on this machine.";
            }

            if (_coordinator.ColorTemperatureRejected)
            {
                return "Windows refused this ramp. It limits how far a gamma ramp may deviate from linear, which is what strongly warm settings run into.";
            }

            return $"{AppSettings.NeutralTemperatureKelvin} K is the neutral white point: no tint is applied.";
        }
    }

    /// <summary>
    /// Whether to offer the extended gamma range. Only shown once Windows has
    /// actually refused a ramp: suggesting a machine-wide registry change and a
    /// sign-out before there is a problem would be scaremongering.
    /// </summary>
    public bool CanOfferGammaRangeUnlock =>
        _gammaRange.CanUnlock && _coordinator.ColorTemperatureRejected;

    public string GammaRangeStatus => _gammaRange.State switch
    {
        GammaRangeState.UnlockedPendingSignOut =>
            "The extended range is set but not yet active. Sign out and back in to apply it.",
        GammaRangeState.Unlocked =>
            "The extended gamma range is active, so the full warmth of the slider is available.",
        GammaRangeState.Restricted =>
            "Windows limits how far a gamma ramp may deviate from linear, which caps how warm any colour tool can go. Lifting it is a machine-wide change that needs administrator rights and a sign-out.",
        _ => string.Empty,
    };

    // --- Schedule ------------------------------------------------------------

    /// <summary>
    /// Whether the manual temperature slider is in charge. While the schedule
    /// is driving, the slider is disabled and shows what the schedule chose —
    /// a control the user can move but that immediately springs back would be
    /// worse than one that plainly says it is not theirs right now.
    /// </summary>
    public bool IsTemperatureManual => !AutomationEnabled;

    /// <summary>Temperature actually on screen, whether manual or scheduled.</summary>
    public int EffectiveTemperatureKelvin => _coordinator.EffectiveTemperatureKelvin;

    public string ScheduleStatus
    {
        get
        {
            if (!AutomationEnabled)
            {
                return "The schedule is off; the slider above is in charge.";
            }

            if (_automation.ActiveOverride is { } paused)
            {
                TimeSpan? left = paused.RemainingAt(DateTimeOffset.Now);
                return left is { } remaining
                    ? $"{paused.Description} — resuming in {Describe(remaining)}."
                    : $"{paused.Description} — until you resume it.";
            }

            if (_automation.CurrentState is not { } state)
            {
                return "Waiting for the first evaluation…";
            }

            string phase = state.Phase switch
            {
                SchedulePhase.Day => "Daytime",
                SchedulePhase.Sunset => "Warming for the evening",
                SchedulePhase.Night => "Night",
                _ => "Cooling for the morning",
            };

            return $"{phase} — {EffectiveTemperatureKelvin} K, next change at {state.NextChangeAt:HH:mm}.";
        }
    }

    /// <summary>
    /// Shown when the schedule is following the clock despite being asked to
    /// follow the sun.
    /// </summary>
    public string? ScheduleFallbackWarning
    {
        get
        {
            if (!AutomationEnabled || !UseSolarTimes)
            {
                return null;
            }

            if (_automation.CurrentState is { UsedSolarTimes: false })
            {
                return _settingsStore.Current.Schedule.Location is null
                    ? "No location set, so the fixed times below are being used instead of the sun."
                    : "The sun does not cross the horizon here today, so the fixed times below are being used.";
            }

            return null;
        }
    }

    public bool HasScheduleFallbackWarning => ScheduleFallbackWarning is not null;

    public bool IsScheduleOverridden => _automation.ActiveOverride is not null;

    /// <summary>Warning text when another colour tool is fighting Redture.</summary>
    public string? ConflictWarning => _coordinator.ColorConflictWarning;

    /// <summary>Drives the visibility of the conflict banner.</summary>
    public bool HasConflict => _coordinator.ColorConflictWarning is not null;

    public string DisplaySummary => Displays.Count switch
    {
        0 => "No displays detected",
        1 => "1 display detected",
        var count => $"{count} displays detected",
    };

    /// <summary>
    /// Asks Windows to lift the gamma range restriction, which raises a UAC
    /// prompt. Never invoked automatically.
    /// </summary>
    [RelayCommand]
    private void UnlockGammaRange()
    {
        _settingsStore.Current.ExtendedGammaRangeOptIn = true;
        _settingsStore.RequestSave();

        _gammaRange.TryRequestUnlock();

        OnPropertyChanged(nameof(GammaRangeStatus));
        OnPropertyChanged(nameof(CanOfferGammaRangeUnlock));
    }

    /// <summary>Re-reads the display topology from the OS.</summary>
    [RelayCommand]
    private void RefreshDisplays()
    {
        Displays.Clear();

        foreach (DisplayInfo display in _displayEnumerator.GetDisplays())
        {
            Displays.Add(display);
        }

        _logger.LogDebug("Control panel refreshed with {Count} display(s).", Displays.Count);
        OnPropertyChanged(nameof(DisplaySummary));
    }

    partial void OnEffectsEnabledChanged(bool value) => Persist(s => s.EffectsEnabled = value);

    partial void OnBrightnessChanged(double value) => Persist(s => s.Brightness = value);

    partial void OnTemperatureKelvinChanged(int value)
    {
        Persist(s => s.TemperatureKelvin = value);

        // The ramp may be refused at one end of the slider and accepted at the
        // other, so the explanation has to be re-read after every change.
        OnPropertyChanged(nameof(TemperatureStatus));
        OnPropertyChanged(nameof(CanOfferGammaRangeUnlock));
    }

    partial void OnAutomationEnabledChanged(bool value)
    {
        Persist(s => s.AutomationEnabled = value);

        if (!_suppressPersist)
        {
            _automation.OnAutomationToggled();
        }

        OnPropertyChanged(nameof(IsTemperatureManual));
        RefreshScheduleStatus();
    }

    /// <summary>Whether starting at logon is even possible here.</summary>
    public bool CanStartWithSystem => _autoStart.IsSupported;

    partial void OnStartWithSystemChanged(bool value)
    {
        if (_suppressPersist)
        {
            return;
        }

        // Reflect what actually happened rather than what was asked for: if the
        // registry write fails, the switch must go back.
        bool applied = _autoStart.SetEnabled(value);

        if (!applied)
        {
            _suppressPersist = true;
            try
            {
                StartWithSystem = _autoStart.IsEnabled;
            }
            finally
            {
                _suppressPersist = false;
            }

            return;
        }

        Persist(s => s.StartWithSystem = value);
    }

    partial void OnDayTemperatureKelvinChanged(int value) => PersistSchedule(s => s.DayTemperatureKelvin = value);

    partial void OnNightTemperatureKelvinChanged(int value) => PersistSchedule(s => s.NightTemperatureKelvin = value);

    partial void OnTransitionMinutesChanged(int value) => PersistSchedule(s => s.TransitionMinutes = value);

    partial void OnUseSolarTimesChanged(bool value) => PersistSchedule(s => s.UseSolarTimes = value);

    partial void OnLatitudeTextChanged(string value) =>
        PersistSchedule(s => s.Latitude = ParseCoordinate(value));

    partial void OnLongitudeTextChanged(string value) =>
        PersistSchedule(s => s.Longitude = ParseCoordinate(value));

    partial void OnSunriseTextChanged(string value) =>
        PersistSchedule(s => s.ManualSunrise = ParseTime(value) ?? s.ManualSunrise);

    partial void OnSunsetTextChanged(string value) =>
        PersistSchedule(s => s.ManualSunset = ParseTime(value) ?? s.ManualSunset);

    [RelayCommand]
    private void PauseForAnHour() => _automation.PauseFor(TimeSpan.FromHours(1), "Paused for an hour");

    [RelayCommand]
    private void PauseUntilSunrise() => _automation.PauseUntilSunrise();

    [RelayCommand]
    private void EnableCinemaMode() => _automation.PauseIndefinitely("Cinema mode");

    [RelayCommand]
    private void ResumeSchedule() => _automation.Resume();

    /// <summary>
    /// Applies a change to the schedule settings and schedules a save. The
    /// schedule is re-evaluated on the automation loop's next tick, so nothing
    /// needs pushing at the display from here.
    /// </summary>
    private void PersistSchedule(Action<ScheduleSettings> change)
    {
        if (_suppressPersist)
        {
            return;
        }

        change(_settingsStore.Current.Schedule);
        _settingsStore.Current.Schedule.Normalize();
        _settingsStore.RequestSave();

        RefreshScheduleStatus();
    }

    /// <summary>
    /// Parses a typed coordinate, treating anything unparseable as "not set"
    /// rather than as zero — which is a real place in the Atlantic and would
    /// silently give someone the wrong sunset.
    /// </summary>
    private static double? ParseCoordinate(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;

    private static TimeOnly? ParseTime(string text) =>
        TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out TimeOnly value) ? value : null;

    private static string Describe(TimeSpan remaining) => remaining switch
    {
        { TotalMinutes: < 1 } => "less than a minute",
        { TotalMinutes: < 60 } => $"{(int)remaining.TotalMinutes} min",
        _ => $"{(int)remaining.TotalHours} h {remaining.Minutes} min",
    };

    private void RefreshScheduleStatus()
    {
        OnPropertyChanged(nameof(ScheduleStatus));
        OnPropertyChanged(nameof(ScheduleFallbackWarning));
        OnPropertyChanged(nameof(HasScheduleFallbackWarning));
        OnPropertyChanged(nameof(IsScheduleOverridden));
        OnPropertyChanged(nameof(EffectiveTemperatureKelvin));
    }

    /// <summary>
    /// Applies a change to the live settings, schedules a save and pushes the
    /// result to the screen.
    /// </summary>
    private void Persist(Action<AppSettings> change)
    {
        if (_suppressPersist)
        {
            return;
        }

        change(_settingsStore.Current);
        _settingsStore.RequestSave();
        _coordinator.Apply();
    }

    /// <summary>
    /// Pulls the current state back into the UI without writing it out again or
    /// re-applying it — whoever changed it has already done that.
    /// </summary>
    private void ReloadFromCoordinator()
    {
        AppSettings settings = _settingsStore.Current;

        _suppressPersist = true;
        try
        {
            EffectsEnabled = settings.EffectsEnabled;
            Brightness = settings.Brightness;
            TemperatureKelvin = settings.TemperatureKelvin;
            AutomationEnabled = settings.AutomationEnabled;
        }
        finally
        {
            _suppressPersist = false;
        }

        OnPropertyChanged(nameof(BacklightSummary));
        OnPropertyChanged(nameof(TemperatureStatus));
        OnPropertyChanged(nameof(ConflictWarning));
        OnPropertyChanged(nameof(HasConflict));
    }

    private static string DescribeMechanism(BrightnessMechanism mechanism) => mechanism switch
    {
        BrightnessMechanism.DdcCi => "DDC/CI",
        BrightnessMechanism.WmiPanel => "built-in panel",
        _ => "unavailable",
    };
}
