using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Gamma;

namespace Redture.App.Services;

/// <summary>
/// Notices when another application is fighting Redture over the display's
/// colour lookup table.
/// </summary>
/// <remarks>
/// <para>
/// The LUT is a single global slot with no ownership and no notification: two
/// applications writing it just take turns, and the user sees the screen
/// flicker between two tints. Redture detects this by writing its ramp and
/// later reading it back — if what comes out is not what went in, something
/// else is writing it.
/// </para>
/// <para>
/// The response is to <em>stop</em>. Re-applying would win the next round and
/// lose the one after, turning a static conflict into a visible ping-pong at
/// whatever rate the two poll at. Reporting it once and stepping back leaves
/// the user with a stable screen and an explanation, which is the only outcome
/// they can actually act on.
/// </para>
/// </remarks>
public sealed class ColorConflictMonitor : IDisposable
{
    /// <summary>
    /// How often to read the LUT back. Deliberately slow: this is a background
    /// sanity check, not a control loop, and a GDI read on a timer is exactly
    /// the kind of thing that should not show up in a tray app's CPU usage.
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How many times to put the ramp back before concluding that something is
    /// genuinely fighting for it.
    /// </summary>
    /// <remarks>
    /// The distinction this whole class turns on. A ramp that is overwritten
    /// once and then left alone is not a conflict — it is a game resetting the
    /// display on its way out, or a driver restoring its own state after a mode
    /// change. Repairing that is correct. A ramp that keeps being taken back
    /// after every repair is a conflict, and repairing that is a flicker loop.
    /// Telling them apart takes an attempt, not a guess.
    /// </remarks>
    private const int RepairAttempts = 3;

    /// <summary>
    /// Window within which repeated repairs count as a fight rather than as
    /// unrelated one-off events.
    /// </summary>
    private static readonly TimeSpan RepairWindow = TimeSpan.FromMinutes(2);

    private readonly IGammaController _gamma;
    private readonly IColorConflictDetector _detector;
    private readonly ILogger<ColorConflictMonitor> _logger;
    private readonly CancellationTokenSource _shutdown = new();

    private Task? _loop;
    private volatile bool _tintApplied;
    private bool _disposed;

    private int _repairsInWindow;
    private DateTimeOffset _firstRepairAt = DateTimeOffset.MinValue;

    public ColorConflictMonitor(
        IGammaController gamma,
        IColorConflictDetector detector,
        ILogger<ColorConflictMonitor> logger)
    {
        _gamma = gamma;
        _detector = detector;
        _logger = logger;
    }

    /// <summary>Raised once, when a conflict is confirmed.</summary>
    public event EventHandler? ConflictDetected;

    /// <summary>Whether another application has been seen overwriting the LUT.</summary>
    public bool HasConflict { get; private set; }

    /// <summary>
    /// Applications identified as also writing the table, empty when none were
    /// recognised.
    /// </summary>
    /// <remarks>
    /// Exposed as names rather than as a finished sentence: the sentence has to
    /// be built in whatever language the interface is currently in, and a
    /// background monitor has no business knowing what that is.
    /// </remarks>
    public IReadOnlyList<string> Applications { get; private set; } = [];

    /// <summary>
    /// Tells the monitor whether a non-neutral ramp is currently applied.
    /// Checking only matters while Redture is actually asking for something:
    /// with no tint applied there is nothing to be overwritten.
    /// </summary>
    public void SetTintApplied(bool applied)
    {
        _tintApplied = applied;

        if (applied && !HasConflict)
        {
            EnsureLoopRunning();
        }
    }

    private void EnsureLoopRunning()
    {
        if (_loop is not null || _disposed)
        {
            return;
        }

        _loop = Task.Run(async () =>
        {
            try
            {
                using PeriodicTimer timer = new(CheckInterval);

                while (await timer.WaitForNextTickAsync(_shutdown.Token).ConfigureAwait(false))
                {
                    if (!_tintApplied)
                    {
                        continue;
                    }

                    if (_gamma.Verify() != GammaVerification.Foreign)
                    {
                        continue;
                    }

                    if (TryRepair())
                    {
                        continue;
                    }

                    Report();
                    return; // Back off rather than start a write war.
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The colour conflict monitor stopped unexpectedly.");
            }
        });
    }

    /// <summary>
    /// Puts the ramp back, and reports whether it is worth continuing to watch.
    /// </summary>
    /// <returns>
    /// True when the repair was made and monitoring should continue; false when
    /// this has happened often enough to be a genuine conflict.
    /// </returns>
    private bool TryRepair()
    {
        DateTimeOffset now = DateTimeOffset.Now;

        // Repairs spread far apart are unrelated incidents, not a fight. Only a
        // burst of them means something is contesting the ramp.
        if (now - _firstRepairAt > RepairWindow)
        {
            _repairsInWindow = 0;
            _firstRepairAt = now;
        }

        if (++_repairsInWindow > RepairAttempts)
        {
            return false;
        }

        _logger.LogInformation(
            "The colour lookup table was changed by something else; putting it back (repair {Count} of {Max}).",
            _repairsInWindow,
            RepairAttempts);

        _gamma.Refresh();
        return true;
    }

    private void Report()
    {
        IReadOnlyList<string> culprits = _detector.FindRunningColorApplications();

        Applications = culprits;
        HasConflict = true;

        _logger.LogWarning(
            "Colour conflict detected. Known colour applications running: {Applications}.",
            culprits.Count > 0 ? string.Join(", ", culprits) : "none identified");

        Dispatcher.UIThread.Post(() => ConflictDetected?.Invoke(this, EventArgs.Empty));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _shutdown.Dispose();
    }
}
