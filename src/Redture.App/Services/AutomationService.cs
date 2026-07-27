using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Redture.Core.Scheduling;
using Redture.Core.Settings;

namespace Redture.App.Services;

/// <summary>
/// Drives the colour temperature from the time of day.
/// </summary>
/// <remarks>
/// <para>
/// All the decisions live in <see cref="ScheduleEvaluator"/> and
/// <see cref="TemperatureSmoother"/>, which are pure and tested against a clock
/// they are handed rather than one they read. What is left here is the part
/// that genuinely cannot be pure: a timer, and pushing the result at the
/// display.
/// </para>
/// <para>
/// The scheduled temperature is deliberately <em>not</em> written back into the
/// saved settings. It changes every few seconds, and persisting it would both
/// hammer the disk and destroy the manual value the user set — which has to be
/// waiting for them, unchanged, when they switch automation off again.
/// </para>
/// </remarks>
public sealed class AutomationService : IDisposable
{
    /// <summary>
    /// Interval while the schedule is steady or moving slowly. A one-hour
    /// sunset moves about a fifth of a mired in five seconds, well below what
    /// an 8-bit ramp can even represent, so most of these ticks compute a ramp
    /// identical to the last one and are discarded before reaching the driver.
    /// </summary>
    private static readonly TimeSpan SteadyInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Interval while catching up to a sudden change. Only ever used for the
    /// second or so after automation is switched on or an override lapses.
    /// </summary>
    private static readonly TimeSpan CatchUpInterval = TimeSpan.FromMilliseconds(50);

    private readonly ISettingsStore _settingsStore;
    private readonly DisplayCoordinator _coordinator;
    private readonly ILogger<AutomationService> _logger;
    private readonly CancellationTokenSource _shutdown = new();

    private readonly TemperatureSmoother _smoother;
    private ScheduleOverride? _override;
    private Task? _loop;
    private bool _disposed;

    public AutomationService(
        ISettingsStore settingsStore,
        DisplayCoordinator coordinator,
        ILogger<AutomationService> logger)
    {
        _settingsStore = settingsStore;
        _coordinator = coordinator;
        _logger = logger;
        _smoother = new TemperatureSmoother(settingsStore.Current.TemperatureKelvin);
    }

    /// <summary>Raised when anything the UI displays about the schedule changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Latest evaluation, or null while automation is off.</summary>
    public ScheduleState? CurrentState { get; private set; }

    /// <summary>Active override, or null when the schedule is in charge.</summary>
    public ScheduleOverride? ActiveOverride => _override;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_loop is not null)
        {
            return;
        }

        _loop = Task.Run(() => RunAsync(_shutdown.Token));
    }

    /// <summary>
    /// Called when the user toggles automation, so the ramp starts from what is
    /// currently on screen instead of jumping from wherever it left off.
    /// </summary>
    public void OnAutomationToggled()
    {
        AppSettings settings = _settingsStore.Current;

        if (settings.AutomationEnabled)
        {
            _smoother.SnapTo(settings.TemperatureKelvin);
        }
        else
        {
            // Hand control back to the manual slider.
            _override = null;
            CurrentState = null;
            _coordinator.SetScheduledTemperature(null);
        }

        RaiseStateChanged();
    }

    /// <summary>Suspends the schedule for a fixed period.</summary>
    public void PauseFor(TimeSpan duration, string description)
    {
        _override = ScheduleOverride.For(DateTimeOffset.Now, duration, description);
        _logger.LogInformation("Schedule overridden: {Description}, until {Expiry:t}.", description, _override.ExpiresAt);
        RaiseStateChanged();
    }

    /// <summary>Suspends the schedule until the next move towards daytime.</summary>
    public void PauseUntilSunrise()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset sunrise = ScheduleEvaluator.NextSunrise(now, _settingsStore.Current.Schedule);

        _override = new ScheduleOverride(sunrise, "Paused until morning");
        _logger.LogInformation("Schedule overridden until sunrise at {Sunrise:t}.", sunrise);
        RaiseStateChanged();
    }

    /// <summary>Suspends the schedule until the user says otherwise.</summary>
    public void PauseIndefinitely(string description)
    {
        _override = ScheduleOverride.Indefinite(description);
        _logger.LogInformation("Schedule overridden indefinitely: {Description}.", description);
        RaiseStateChanged();
    }

    /// <summary>Cancels any override and hands the schedule back control.</summary>
    public void Resume()
    {
        if (_override is null)
        {
            return;
        }

        _override = null;
        _logger.LogInformation("Schedule override cancelled.");
        RaiseStateChanged();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset lastTick = DateTimeOffset.Now;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                DateTimeOffset now = DateTimeOffset.Now;
                TimeSpan elapsed = now - lastTick;
                lastTick = now;

                bool catchingUp = false;

                if (_settingsStore.Current.AutomationEnabled)
                {
                    catchingUp = Tick(now, elapsed);
                }

                await Task.Delay(catchingUp ? CatchUpInterval : SteadyInterval, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The automation loop stopped unexpectedly.");
        }
    }

    /// <summary>
    /// Evaluates the schedule once and pushes the result. Returns whether the
    /// smoother still has ground to cover, which decides how soon to come back.
    /// </summary>
    private bool Tick(DateTimeOffset now, TimeSpan elapsed)
    {
        AppSettings settings = _settingsStore.Current;

        // A lapsed override is noticed here rather than on a timer of its own:
        // the loop is already running, and an override that expires while the
        // machine was asleep should take effect on the first tick after it
        // wakes, not an hour later.
        if (_override is { } active && !active.IsActiveAt(now))
        {
            _override = null;
            _logger.LogInformation("Schedule override expired.");
            RaiseStateChanged();
        }

        ScheduleState state = ScheduleEvaluator.Evaluate(now, settings.Schedule);
        SchedulePhase? previousPhase = CurrentState?.Phase;
        CurrentState = state;

        int target = _override is not null
            ? settings.Schedule.DayTemperatureKelvin
            : state.TemperatureKelvin;

        int applied = _smoother.Advance(target, elapsed);
        _coordinator.SetScheduledTemperature(applied);

        if (previousPhase != state.Phase)
        {
            _logger.LogInformation(
                "Schedule phase is now {Phase}, target {Target} K, next change at {Next:t}.",
                state.Phase,
                target,
                state.NextChangeAt);
        }

        RaiseStateChanged();
        return !_smoother.HasSettledAt(target);
    }

    private void RaiseStateChanged() =>
        Dispatcher.UIThread.Post(() => StateChanged?.Invoke(this, EventArgs.Empty));

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
