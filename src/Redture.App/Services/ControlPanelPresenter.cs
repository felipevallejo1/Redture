using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redture.App.ViewModels;
using Redture.App.Views;

namespace Redture.App.Services;

/// <summary>
/// Owns the single control-panel window instance.
/// </summary>
/// <remarks>
/// The window is created lazily and then kept alive and hidden rather than
/// destroyed: Redture spends almost all of its life in the tray, and recreating
/// a window (plus its view model and bindings) on every open would be both
/// slower and a source of subtle state loss.
/// </remarks>
public sealed class ControlPanelPresenter
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ControlPanelPresenter> _logger;

    private ControlPanelWindow? _window;
    private bool _shuttingDown;

    public ControlPanelPresenter(IServiceProvider services, ILogger<ControlPanelPresenter> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>Shows and focuses the panel, creating it on first use.</summary>
    public void Show()
    {
        if (_shuttingDown)
        {
            return;
        }

        ControlPanelWindow window = EnsureWindow();

        // Refresh on open instead of polling: display topology can change while
        // the panel is hidden, and nothing is watching for it yet in stage 0.
        (window.DataContext as ControlPanelViewModel)?.RefreshDisplaysCommand.Execute(null);

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    /// <summary>Hides the panel if visible, shows it otherwise.</summary>
    public void Toggle()
    {
        if (_window is { IsVisible: true })
        {
            _window.Hide();
            return;
        }

        Show();
    }

    /// <summary>
    /// Allows the window to actually close instead of hiding. Called only from
    /// <see cref="ApplicationLifecycle"/>.
    /// </summary>
    public void PrepareForShutdown()
    {
        _shuttingDown = true;
        _window?.Close();
    }

    private ControlPanelWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        _logger.LogDebug("Creating the control panel window.");

        _window = new ControlPanelWindow
        {
            DataContext = _services.GetRequiredService<ControlPanelViewModel>(),
        };

        _window.Closing += (_, e) =>
        {
            if (_shuttingDown)
            {
                return; // Real shutdown: let it close.
            }

            // Closing the panel must not quit the app — that is what the tray
            // menu's Exit item is for.
            e.Cancel = true;
            _window?.Hide();
        };

        return _window;
    }
}
