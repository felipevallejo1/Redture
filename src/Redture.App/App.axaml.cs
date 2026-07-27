using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Redture.App.Infrastructure;
using Redture.App.Services;

namespace Redture.App;

/// <summary>
/// The Avalonia application object. Kept deliberately thin: it wires the tray
/// icon up and hands everything else to the services resolved in
/// <see cref="Infrastructure.AppServices"/>.
/// </summary>
public sealed partial class App : Application
{
    private readonly IServiceProvider? _services;

    /// <summary>
    /// Parameterless constructor required by <c>AppBuilder.Configure&lt;TApp&gt;</c>
    /// and used by the XAML previewer, which has no service container. When
    /// <see cref="_services"/> is null the app renders but wires up nothing.
    /// </summary>
    public App()
        : this(null)
    {
    }

    public App(IServiceProvider? services) => _services = services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (_services is not null && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Redture has no main window: closing the panel must not quit, and
            // the process is expected to outlive every window it opens.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Started before the tray icon so the stored brightness is already
            // on screen by the time the user can interact with anything.
            _services.GetRequiredService<DisplayCoordinator>().Start();
            _services.GetRequiredService<AutomationService>().Start();

            // A log-off or power-off must not look like a crash on the next
            // boot, so the session-end path runs the same teardown the tray's
            // Exit item does.
            desktop.ShutdownRequested += (_, _) =>
                _services.GetRequiredService<ApplicationLifecycle>().ShutdownForSessionEnd();

            _services.GetRequiredService<TrayIconService>().Initialize();

            // Launching Redture again while it is running should bring the
            // panel up rather than appear to do nothing.
            ControlPanelPresenter presenter = _services.GetRequiredService<ControlPanelPresenter>();
            _services.GetRequiredService<SingleInstanceGuard>().ActivationRequested +=
                (_, _) => Dispatcher.UIThread.Post(presenter.Show);

            if (_services.GetRequiredService<StartupOptions>().ShowPanelOnStart)
            {
                _services.GetRequiredService<ControlPanelPresenter>().Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
