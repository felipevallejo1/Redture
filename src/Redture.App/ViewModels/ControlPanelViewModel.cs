using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Redture.App.Infrastructure;
using Redture.Core.Infrastructure;
using Redture.Core.Settings;
using Redture.Platform.Abstractions.Displays;

namespace Redture.App.ViewModels;

/// <summary>
/// Backing view model for the control panel.
/// </summary>
/// <remarks>
/// The view model is the only thing that writes to <see cref="AppSettings"/>
/// from the UI. Every change goes through <see cref="Persist"/>, which mutates
/// the live settings object and asks the store for a debounced save — dragging
/// a slider therefore produces exactly one disk write, 750 ms after the user
/// lets go.
/// <para>
/// Stage 0 stops there: the values round-trip to disk but nothing applies them
/// to the screen yet. The overlay (stage 1) and the gamma ramp (stage 2) attach
/// to the same properties.
/// </para>
/// </remarks>
public sealed partial class ControlPanelViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly IDisplayEnumerator _displayEnumerator;
    private readonly IAppPaths _paths;
    private readonly ILogger<ControlPanelViewModel> _logger;

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
        IAppPaths paths,
        ILogger<ControlPanelViewModel> logger)
    {
        _settingsStore = settingsStore;
        _displayEnumerator = displayEnumerator;
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

        // Displays are not enumerated here: ControlPanelPresenter refreshes them
        // every time the panel is opened, which is the only moment the list is
        // actually looked at.
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

    public string DisplaySummary => Displays.Count switch
    {
        0 => "No displays detected",
        1 => "1 display detected",
        var count => $"{count} displays detected",
    };

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

    partial void OnTemperatureKelvinChanged(int value) => Persist(s => s.TemperatureKelvin = value);

    partial void OnAutomationEnabledChanged(bool value) => Persist(s => s.AutomationEnabled = value);

    /// <summary>Applies a change to the live settings and schedules a save.</summary>
    private void Persist(Action<AppSettings> change)
    {
        change(_settingsStore.Current);
        _settingsStore.RequestSave();
    }
}
