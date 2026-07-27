using System.Reflection;
using Redture.Core.Infrastructure;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Redture.App.Infrastructure;

/// <summary>
/// Builds the Serilog pipeline. Logging is configured before the DI container
/// exists so that container construction itself can be logged.
/// </summary>
internal static class LoggingSetup
{
    /// <summary>Application version, read once from the assembly metadata.</summary>
    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";

    public static Logger CreateLogger(IAppPaths paths)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(DefaultLevel)
            .Enrich.WithProperty("Version", Version)
            .WriteTo.File(
                path: Path.Combine(paths.LogDirectory, "redture-.log"),
                rollingInterval: RollingInterval.Day,
                // A tray app runs for weeks; cap the log folder so it cannot
                // grow without bound on a user's machine.
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 8L * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            // Only visible under `dotnet run`; harmless when launched from Explorer.
            .WriteTo.Console()
            .CreateLogger();
    }

    private static LogEventLevel DefaultLevel =>
#if DEBUG
        LogEventLevel.Debug;
#else
        LogEventLevel.Information;
#endif
}
