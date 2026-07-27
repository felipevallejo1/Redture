using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Redture.App.Infrastructure;
using Redture.Core.Infrastructure;
using Redture.Core.Settings;
using Redture.Platform.Windows.Gamma;
using Serilog;

namespace Redture.App;

/// <summary>
/// Process entry point: single-instance check, logging, container, UI.
/// </summary>
internal static class Program
{
    /// <summary>
    /// STA is required by the Windows shell APIs the tray icon sits on.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        // Handled before the single-instance check on purpose: this switch runs
        // in a short-lived elevated process launched by the instance already
        // running, which would otherwise turn it away at the door.
        if (StartupOptions.IsGammaRangeUnlockRequest(args))
        {
            return RunGammaRangeUnlock();
        }

        // Before anything else: refuse to run twice. Two instances would each
        // apply their own display corrections on top of the other's.
        using SingleInstanceGuard? instance = SingleInstanceGuard.TryAcquire();
        if (instance is null)
        {
            return 0;
        }

        IAppPaths paths = AppPaths.CreateDefault();
        paths.EnsureCreated();
        Log.Logger = LoggingSetup.CreateLogger(paths);

        ServiceProvider? services = null;

        try
        {
            Log.Information(
                "Redture {Version} starting. OS: {OperatingSystem}. Data: {DataDirectory}",
                LoggingSetup.Version,
                RuntimeInformation.OSDescription,
                paths.DataDirectory);

            InstallCrashHandlers();

            services = AppServices.Build(paths, StartupOptions.Parse(args), instance);
            services.GetRequiredService<CleanShutdownSentinel>().BeginRun();

            // Settings must be on disk-loaded before the UI binds to them, so
            // this one blocking wait during startup is intentional.
            services.GetRequiredService<ISettingsStore>().LoadAsync().GetAwaiter().GetResult();

            BuildAvaloniaApp(services)
                .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Redture terminated unexpectedly.");
            return 1;
        }
        finally
        {
            // Defence in depth: ApplicationLifecycle already does this on the
            // normal path, but an exception can bypass it.
            TryFlushSettings(services);
            services?.Dispose();

            Log.Information("Redture stopped.");
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Writes the machine-wide registry value that lifts Windows' restriction
    /// on gamma ramps, then exits.
    /// </summary>
    /// <remarks>
    /// Runs as its own elevated process so that Redture itself never has to run
    /// as administrator. It touches nothing else: no UI, no settings, no
    /// display state.
    /// </remarks>
    private static int RunGammaRangeUnlock()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 1;
        }

        bool applied = WindowsGammaRangeUnlock.ApplyElevated(out string message);
        Console.WriteLine(message);
        return applied ? 0 : 1;
    }

    /// <summary>
    /// Entry point used by the Avalonia XAML previewer, which calls a
    /// parameterless <c>BuildAvaloniaApp</c> by convention.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(null);

    private static AppBuilder BuildAvaloniaApp(IServiceProvider? services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Catches what would otherwise be silent deaths. A tray app has no console
    /// and often no visible window, so an unlogged crash is invisible to the
    /// user — and leaves a tinted screen behind, since the gamma ramp outlives
    /// the process that set it.
    /// </summary>
    private static void InstallCrashHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating}).", e.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception.");
            e.SetObserved();
        };
    }

    private static void TryFlushSettings(ServiceProvider? services)
    {
        if (services is null)
        {
            return;
        }

        try
        {
            services.GetRequiredService<ISettingsStore>().FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Final settings flush failed.");
        }
    }
}
