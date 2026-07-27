using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redture.App.Services;
using Redture.App.ViewModels;
using Redture.Core;
using Redture.Core.Infrastructure;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Overlay;
using Redture.Platform.Abstractions.SystemEvents;
using Redture.Platform.Windows;
using Serilog;

namespace Redture.App.Infrastructure;

/// <summary>
/// The composition root: the single place where concrete types are wired to
/// their interfaces.
/// </summary>
internal static class AppServices
{
    public static ServiceProvider Build(IAppPaths paths, StartupOptions startupOptions)
    {
        ServiceCollection services = new();

        // Reuse the paths instance created during logging bootstrap rather than
        // letting Core build a second one.
        services.AddSingleton(paths);
        services.AddSingleton(startupOptions);

        services.AddLogging(builder => builder
            .ClearProviders()
            .AddSerilog(Log.Logger, dispose: false));

        services.AddRedtureCore();
        AddPlatformServices(services);

        services.AddSingleton<DisplayCoordinator>();
        services.AddSingleton<ApplicationLifecycle>();
        services.AddSingleton<ControlPanelPresenter>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<ControlPanelViewModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    /// <summary>
    /// Selects the OS backend. The <c>OperatingSystem.IsWindows()</c> guard is
    /// not decoration: it is what lets the platform-compatibility analyzer prove
    /// the Windows-only registration is unreachable elsewhere, so this assembly
    /// still compiles and runs on Linux and macOS.
    /// </summary>
    private static void AddPlatformServices(IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            services.AddWindowsPlatform();
            return;
        }

        // Linux (stage 5) and macOS (stage 6) fall back to no-op backends so
        // the app still starts and can explain what is unavailable.
        services.AddSingleton<IDisplayEnumerator, UnsupportedDisplayEnumerator>();
        services.AddSingleton<IOverlayController, NullOverlayController>();
        services.AddSingleton<ISystemEvents, NullSystemEvents>();
    }
}
