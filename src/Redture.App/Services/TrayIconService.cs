using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;

namespace Redture.App.Services;

/// <summary>
/// Creates and owns the system-tray icon, which is Redture's primary — and for
/// most of its runtime, only — user interface.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const string IconAssetUri = "avares://Redture/Assets/redture.ico";

    private readonly ControlPanelPresenter _presenter;
    private readonly ApplicationLifecycle _lifecycle;
    private readonly ILogger<TrayIconService> _logger;

    private TrayIcon? _trayIcon;

    public TrayIconService(
        ControlPanelPresenter presenter,
        ApplicationLifecycle lifecycle,
        ILogger<TrayIconService> logger)
    {
        _presenter = presenter;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    /// <summary>Builds the icon and its menu. Must run on the UI thread.</summary>
    public void Initialize()
    {
        NativeMenuItem openItem = new("Open Redture");
        openItem.Click += (_, _) => _presenter.Show();

        NativeMenuItem exitItem = new("Exit");
        exitItem.Click += OnExitClicked;

        NativeMenu menu = new();
        menu.Add(openItem);
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

        _logger.LogInformation("Tray icon initialised.");
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
