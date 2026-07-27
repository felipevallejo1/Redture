namespace Redture.Platform.Abstractions.Gamma;

/// <summary>
/// Reads, and optionally lifts, the operating system's restriction on how far a
/// gamma ramp may deviate from linear.
/// </summary>
/// <remarks>
/// <para>
/// Windows applies this restriction so that no application can black out or
/// wildly distort the display without the user being able to see what happened
/// — a reasonable default that also caps how warm any colour tool can go.
/// </para>
/// <para>
/// Lifting it is a machine-wide registry change requiring administrator rights
/// and a sign-out. Redture therefore never does it silently: it reports the
/// state, explains the trade-off, and only acts when the user explicitly asks.
/// </para>
/// </remarks>
public interface IGammaRangeUnlock
{
    /// <summary>Current state, as of the last <see cref="Refresh"/>.</summary>
    GammaRangeState State { get; }

    /// <summary>Whether unlocking is even possible on this platform.</summary>
    bool CanUnlock { get; }

    /// <summary>Re-reads the state from the system.</summary>
    void Refresh();

    /// <summary>
    /// Asks the OS to apply the change, elevating if necessary. Returns whether
    /// the request was successfully started — not whether the user approved it,
    /// which is only visible on the next <see cref="Refresh"/>.
    /// </summary>
    bool TryRequestUnlock();
}
