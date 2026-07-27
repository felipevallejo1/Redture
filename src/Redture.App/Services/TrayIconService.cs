using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Redture.Core.Settings;

namespace Redture.App.Services;

/// <summary>
/// Creates and owns the system-tray icon, which is Redture's primary — and for
/// most of its runtime, only — user interface.
/// </summary>
/// <remarks>
/// The menu carries the two actions people actually reach for without wanting
/// to open a window: switching the corrections off, and buying an hour of
/// normal colour. Everything else is a setting, and settings belong in the
/// panel.
/// </remarks>
public sealed class TrayIconService : IDisposable
{
    private const string IconAssetUri = "avares://Redture/Assets/redture.ico";

    private readonly ControlPanelPresenter _presenter;
    private readonly ApplicationLifecycle _lifecycle;
    private readonly DisplayCoordinator _coordinator;
    private readonly AutomationService _automation;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<TrayIconService> _logger;

    private TrayIcon? _trayIcon;
    private NativeMenuItem? _toggleItem;
    private NativeMenuItem? _pauseItem;

    public TrayIconService(
        ControlPanelPresenter presenter,
        ApplicationLifecycle lifecycle,
        DisplayCoordinator coordinator,
        AutomationService automation,
        ISettingsStore settingsStore,
        ILogger<TrayIconService> logger)
    {
        _presenter = presenter;
        _lifecycle = lifecycle;
        _coordinator = coordinator;
        _automation = automation;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    /// <summary>Builds the icon and its menu. Must run on the UI thread.</summary>
    public void Initialize()
    {
        NativeMenuItem openItem = new("Open Redture");
        openItem.Click += (_, _) => _presenter.Show();

        _toggleItem = new NativeMenuItem("Turn corrections off");
        _toggleItem.Click += (_, _) => ToggleCorrections();

        _pauseItem = new NativeMenuItem("Pause schedule for an hour");
        _pauseItem.Click += (_, _) => PauseForAnHour();

        NativeMenuItem exitItem = new("Exit");
        exitItem.Click += OnExitClicked;

        NativeMenu menu = new();
        menu.Add(openItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(_toggleItem);
        menu.Add(_pauseItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = LoadIcon(),
            ToolTipText = "Redture",
            Menu = menu,
            IsVisible = true,
        };

        // Left-click toggles the panel; the menu stays on right-click.
        _trayIcon.Clicked += (_, _) => _presenter.Toggle();

        // Registering the icon on the Application makes Avalonia dispose it as
        // part of normal shutdown, which is what removes it from the tray.
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _trayIcon });

        _coordinator.ExternalStateChanged += (_, _) => Refresh();
        _automation.StateChanged += (_, _) => Refresh();
        Refresh();

        _logger.LogInformation("Tray icon initialised.");
    }

    /// <summary>
    /// Brings the tooltip and menu wording in line with the current state, so
    /// hovering the icon answers "is this doing anything right now" without a
    /// click.
    /// </summary>
    public void Refresh()
    {
        if (_trayIcon is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            AppSettings settings = _settingsStore.Current;

            if (_toggleItem is not null)
            {
                _toggleItem.Header = settings.EffectsEnabled ? "Turn corrections off" : "Turn corrections on";
            }

            if (_pauseItem is not null)
            {
                _pauseItem.IsEnabled = settings.AutomationEnabled;
                _pauseItem.Header = _automation.ActiveOverride is { } paused
                    ? $"Resume schedule ({paused.Description.ToLowerInvariant()})"
                    : "Pause schedule for an hour";
            }

            _trayIcon.ToolTipText = settings.EffectsEnabled
                ? $"Redture — {_coordinator.EffectiveTemperatureKelvin} K, brightness {settings.Brightness:0}%"
                : "Redture — corrections off";
        });
    }

    private void ToggleCorrections()
    {
        _settingsStore.Current.EffectsEnabled = !_settingsStore.Current.EffectsEnabled;
        _settingsStore.RequestSave();
        _coordinator.Apply();
        Refresh();
    }

    /// <summary>
    /// One menu entry covering both directions: whatever the schedule is doing,
    /// this puts it right for the next hour or hands it back.
    /// </summary>
    private void PauseForAnHour()
    {
        if (_automation.ActiveOverride is not null)
        {
            _automation.Resume();
        }
        else
        {
            _automation.PauseFor(TimeSpan.FromHours(1), "Paused for an hour");
        }

        Refresh();
    }

    private async void OnExitClicked(object? sender, EventArgs e)
    {
        try
        {
            await _lifecycle.ShutdownAsync();
        }
        catch (Exception ex)
        {
            // Last resort: the user asked to quit, so quit even if the tidy
            // path failed.
            _logger.LogCritical(ex, "Graceful shutdown failed; terminating.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Loads the tray icon from embedded resources. A missing or unreadable
    /// asset must not prevent the app from starting — it would leave the user
    /// with a running process and no way to reach it — so failures degrade to
    /// the platform's default icon.
    /// </summary>
    private WindowIcon? LoadIcon()
    {
        try
        {
            using Stream stream = AssetLoader.Open(new Uri(IconAssetUri));
            return new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load the tray icon from {Uri}.", IconAssetUri);
            return null;
        }
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
