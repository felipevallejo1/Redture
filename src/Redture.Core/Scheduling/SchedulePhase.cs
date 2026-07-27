namespace Redture.Core.Scheduling;

/// <summary>Where the schedule currently sits.</summary>
public enum SchedulePhase
{
    /// <summary>Holding the daytime temperature.</summary>
    Day,

    /// <summary>Warming, between the sunset anchor and the end of its transition.</summary>
    Sunset,

    /// <summary>Holding the night-time temperature.</summary>
    Night,

    /// <summary>Cooling, between the sunrise anchor and the end of its transition.</summary>
    Sunrise,
}
