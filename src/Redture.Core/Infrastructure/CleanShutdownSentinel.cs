using Microsoft.Extensions.Logging;

namespace Redture.Core.Infrastructure;

/// <summary>
/// Detects whether the previous run ended abnormally.
/// </summary>
/// <remarks>
/// This matters because a gamma ramp is global driver state that outlives the
/// process that set it: if Redture is killed while a warm ramp is applied, the
/// screen stays orange with nothing on screen to explain why. The sentinel file
/// lets the next launch notice the crash and force the ramp back to linear.
/// </remarks>
public sealed class CleanShutdownSentinel
{
    private readonly IAppPaths _paths;
    private readonly ILogger<CleanShutdownSentinel> _logger;

    public CleanShutdownSentinel(IAppPaths paths, ILogger<CleanShutdownSentinel> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    /// <summary>
    /// True when the sentinel from the previous run was still present at
    /// startup, i.e. that run never exited gracefully.
    /// </summary>
    public bool PreviousRunWasUnclean { get; private set; }

    /// <summary>
    /// Records that a run is in progress and reports whether the previous one
    /// crashed. Safe to call once per process, at startup.
    /// </summary>
    public bool BeginRun()
    {
        try
        {
            _paths.EnsureCreated();
            PreviousRunWasUnclean = File.Exists(_paths.CleanShutdownSentinelPath);

            if (PreviousRunWasUnclean)
            {
                _logger.LogWarning(
                    "The previous run did not shut down cleanly; display state will be reset to a safe baseline.");
            }

            File.WriteAllText(
                _paths.CleanShutdownSentinelPath,
                $"pid={Environment.ProcessId} started={DateTimeOffset.UtcNow:O}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Never block startup over the crash marker.
            _logger.LogWarning(ex, "Could not write the shutdown sentinel; crash detection is disabled this run.");
        }

        return PreviousRunWasUnclean;
    }

    /// <summary>Clears the marker. Called on the graceful shutdown path only.</summary>
    public void EndRun()
    {
        try
        {
            File.Delete(_paths.CleanShutdownSentinelPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not remove the shutdown sentinel.");
        }
    }
}
