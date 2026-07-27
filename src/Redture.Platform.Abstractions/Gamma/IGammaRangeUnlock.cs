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

    /// <summary>
    /// The exact command an administrator would run to apply the change, or
    /// null where the concept does not apply.
    /// </summary>
    /// <remarks>
    /// Deliberately a command for the user to run rather than something Redture
    /// does on their behalf.
    /// <para>
    /// An application that relaunches itself elevated turns a user-writable
    /// install directory into a privilege escalation: anything already running
    /// as the user can replace the executable, and the next elevation prompt —
    /// which the user approves believing it is this application — runs the
    /// replacement as administrator. Handing over a command the user can read
    /// and run themselves removes that primitive, and shows them exactly what
    /// is about to change.
    /// </para>
    /// </remarks>
    string? UnlockCommand { get; }

    /// <summary>Re-reads the state from the system.</summary>
    void Refresh();
}
