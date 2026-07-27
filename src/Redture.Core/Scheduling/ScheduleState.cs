namespace Redture.Core.Scheduling;

/// <summary>
/// What the schedule asks for at one instant.
/// </summary>
/// <param name="TemperatureKelvin">Colour temperature the schedule wants now.</param>
/// <param name="Phase">Which part of the cycle produced it.</param>
/// <param name="NextChangeAt">
/// When this stops being true: the end of the current transition, or the start
/// of the next one.
/// </param>
/// <param name="UsedSolarTimes">
/// Whether the anchors came from the sun's position or from the fixed fallback
/// times. Surfaced so the UI never claims to be following the sun when it
/// quietly is not.
/// </param>
public readonly record struct ScheduleState(
    int TemperatureKelvin,
    SchedulePhase Phase,
    DateTimeOffset NextChangeAt,
    bool UsedSolarTimes)
{
    public bool IsTransitioning => Phase is SchedulePhase.Sunset or SchedulePhase.Sunrise;
}
