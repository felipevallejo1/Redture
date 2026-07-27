using Redture.Core.Color;

namespace Redture.Core.Scheduling;

/// <summary>
/// Rate-limits how fast the colour temperature is allowed to move.
/// </summary>
/// <remarks>
/// <para>
/// The schedule already changes slowly, so during a sunset this does nothing
/// at all — each step is far below the limit and passes through untouched. It
/// earns its place at the moments the schedule <em>jumps</em>: enabling
/// automation in the middle of the night, an override lapsing, the panic
/// hotkey. Those would otherwise snap the whole screen in one frame.
/// </para>
/// <para>
/// One mechanism covers both, with no branch deciding which case it is. That
/// is the point: a rule that applies always cannot be applied inconsistently.
/// </para>
/// </remarks>
public sealed class TemperatureSmoother
{
    /// <summary>
    /// Fastest the temperature may move, mired per second.
    /// </summary>
    /// <remarks>
    /// Chosen so the widest possible jump — the full slider, roughly 1100 mired
    /// — takes about a second and a half: fast enough to feel immediate, slow
    /// enough not to flash.
    /// </remarks>
    public const double DefaultMiredPerSecond = 750d;

    private readonly double _maxMiredPerSecond;

    private double _currentMired;

    public TemperatureSmoother(int initialKelvin, double maxMiredPerSecond = DefaultMiredPerSecond)
    {
        _maxMiredPerSecond = maxMiredPerSecond > 0d ? maxMiredPerSecond : DefaultMiredPerSecond;
        _currentMired = Mired.FromKelvin(initialKelvin);
    }

    /// <summary>Temperature currently being shown.</summary>
    public int CurrentKelvin => (int)Math.Round(Mired.ToKelvin(_currentMired));

    /// <summary>Jumps straight to a temperature, skipping the ramp entirely.</summary>
    public void SnapTo(int kelvin) => _currentMired = Mired.FromKelvin(kelvin);

    /// <summary>Whether the current value has reached a target.</summary>
    public bool HasSettledAt(int targetKelvin) => CurrentKelvin == targetKelvin;

    /// <summary>
    /// Moves towards <paramref name="targetKelvin"/> by at most the configured
    /// rate, and returns the new value.
    /// </summary>
    public int Advance(int targetKelvin, TimeSpan elapsed)
    {
        double target = Mired.FromKelvin(targetKelvin);
        double delta = target - _currentMired;

        double allowed = _maxMiredPerSecond * Math.Max(elapsed.TotalSeconds, 0d);

        // Close enough to finish in one step: land exactly on the target rather
        // than approaching it forever and leaving the caller unable to tell
        // whether the transition has ended.
        _currentMired = Math.Abs(delta) <= allowed
            ? target
            : _currentMired + (Math.Sign(delta) * allowed);

        return CurrentKelvin;
    }
}
