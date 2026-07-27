using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Redture.App.Services;
using Redture.App.ViewModels;
using Redture.Core;
using Redture.Core.Infrastructure;
using Redture.Platform.Abstractions.Brightness;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Gamma;
using Redture.Platform.Abstractions.Overlay;
using Redture.Platform.Abstractions.Startup;
using Redture.Platform.Abstractions.SystemEvents;
using Redture.Platform.Linux;
using Redture.Platform.Windows;
using Serilog;

namespace Redture.App.Infrastructure;

/// <summary>
/// The composition root: the single place where concrete types are wired to
/// their interfaces.
/// </summary>
internal static class AppServices
{
    public static ServiceProvider Build(IAppPaths paths, StartupOptions startupOptions, SingleInstanceGuard instance)
    {
        ServiceCollection services = new();

        services.AddSingleton(instance);

        // Reuse the paths instance created during logging bootstrap rather than
        // letting Core build a second one.
        services.AddSingleton(paths);
        services.AddSingleton(startupOptions);

        services.AddLogging(builder => builder
            .ClearProviders()
            .AddSerilog(Log.Logger, dispose: false));

        services.AddRedtureCore();
        AddPlatformServices(services);

        services.AddSingleton<ColorConflictMonitor>();
        services.AddSingleton<DisplayCoordinator>();
        services.AddSingleton<AutomationService>();
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

        if (OperatingSystem.IsLinux())
        {
            // Registers what X11 can deliver. Anything it cannot — the dimming
            // overlay, backlight control, the panic hotkey — falls through to
            // the null backends registered below, so the app runs with a
            // smaller feature set rather than not running.
            services.AddLinuxPlatform();
        }

        // Fallbacks for everything a platform module did not provide. TryAdd, so
        // a real backend registered above always wins and only the gaps get
        // filled — which is what lets the Linux module register the two things
        // X11 can do and inherit honest no-ops for the rest.
        services.TryAddSingleton<IDisplayEnumerator, UnsupportedDisplayEnumerator>();
        services.TryAddSingleton<IHdrDetector, NullHdrDetector>();
        services.TryAddSingleton<IOverlayController, NullOverlayController>();
        services.TryAddSingleton<IHardwareBrightnessController, NullHardwareBrightnessController>();
        services.TryAddSingleton<IGammaController, NullGammaController>();
        services.TryAddSingleton<IColorConflictDetector, NullColorConflictDetector>();
        services.TryAddSingleton<IGammaRangeUnlock, NullGammaRangeUnlock>();
        services.TryAddSingleton<ISystemEvents, NullSystemEvents>();
        services.TryAddSingleton<IFullscreenDetector, NullFullscreenDetector>();
        services.TryAddSingleton<IAutoStartService, NullAutoStartService>();
    }
}
