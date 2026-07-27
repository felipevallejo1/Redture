using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Redture.App.Infrastructure;
using Redture.App.Services;
using Redture.Core.Infrastructure;
using Redture.Core.Settings;
using Redture.Platform.Abstractions.Brightness;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Gamma;

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
    private readonly IGammaRangeUnlock _gammaRange;
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

    public ControlPanelViewModel(
        ISettingsStore settingsStore,
        IDisplayEnumerator displayEnumerator,
        DisplayCoordinator coordinator,
        IGammaRangeUnlock gammaRange,
        IAppPaths paths,
        ILogger<ControlPanelViewModel> logger)
    {
        _settingsStore = settingsStore;
        _displayEnumerator = displayEnumerator;
        _coordinator = coordinator;
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

    partial void OnAutomationEnabledChanged(bool value) => Persist(s => s.AutomationEnabled = value);

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
