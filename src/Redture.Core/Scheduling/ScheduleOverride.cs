namespace Redture.Core.Scheduling;

/// <summary>
/// A temporary suspension of the schedule.
/// </summary>
/// <param name="ExpiresAt">
/// When it lapses on its own, or null to run until the user cancels it.
/// </param>
/// <param name="Description">Short label for the UI, e.g. "Paused for an hour".</param>
/// <remarks>
/// Overrides exist because the schedule is right almost all the time and wrong
/// occasionally — watching a film, grading photographs, or simply working late
/// and not wanting the screen to argue about it. Suspending returns the display
/// to the daytime setting rather than switching Redture off, so brightness and
/// everything else keep working.
/// </remarks>
public sealed record ScheduleOverride(DateTimeOffset? ExpiresAt, string Description)
{
    /// <summary>Lapses on its own after <paramref name="duration"/>.</summary>
    public static ScheduleOverride For(DateTimeOffset now, TimeSpan duration, string description) =>
        new(now + duration, description);

    /// <summary>Runs until explicitly cancelled — "cinema mode".</summary>
    public static ScheduleOverride Indefinite(string description) => new(null, description);

    public bool IsActiveAt(DateTimeOffset now) => ExpiresAt is null || now < ExpiresAt.Value;

    /// <summary>Time left, or null when it does not expire on its own.</summary>
    public TimeSpan? RemainingAt(DateTimeOffset now) =>
        ExpiresAt is { } expiry ? Max(expiry - now, TimeSpan.Zero) : null;

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left > right ? left : right;
}
