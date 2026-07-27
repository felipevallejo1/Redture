using Redture.Core.Color;

namespace Redture.Platform.Abstractions.Gamma;

/// <summary>
/// Applies a colour lookup table to the attached displays.
/// </summary>
/// <remarks>
/// <para>
/// The LUT sits in the display controller and is applied during scanout, after
/// everything has been composited. That is why colour temperature goes here
/// rather than through the dimming overlay: it is a per-channel transform with
/// no compositing cost and, crucially, no effect on the black level.
/// </para>
/// <para>
/// Unlike the overlay, this is <em>global driver state</em>. It outlives the
/// process that set it, so a crash leaves the screen tinted with nothing on
/// screen to explain why. Every implementation must therefore be paired with a
/// recovery path — see <c>CleanShutdownSentinel</c>.
/// </para>
/// </remarks>
public interface IGammaController : IDisposable
{
    /// <summary>
    /// Whether at least one display accepted a ramp. False means colour
    /// temperature simply cannot be delivered on this machine.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// True when the OS refused the last ramp. On Windows this is usually the
    /// gamma range restriction rejecting a strongly warm setting; the UI uses
    /// it to explain why nothing happened instead of leaving the user guessing.
    /// </summary>
    bool LastRampRejected { get; }

    /// <summary>
    /// Applies a ramp to every attached display. Sending a ramp identical to
    /// the one already applied is a no-op, which is the main defence against
    /// the flicker this class of tool is known for.
    /// </summary>
    void Apply(GammaRamp ramp);

    /// <summary>
    /// Re-sends the current ramp, discarding any assumption about what the OS
    /// still has. Needed whenever Windows resets the LUT behind our back: a
    /// session unlock, a display change, a secure-desktop prompt.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Forces every display back to the identity ramp. The recovery path after
    /// an unclean shutdown, and the last thing that runs before exit.
    /// </summary>
    void ResetToLinear();

    /// <summary>
    /// Reads the table back out of the driver and reports whether it is still
    /// the one Redture wrote.
    /// </summary>
    /// <remarks>
    /// This is how a conflict with another colour tool is detected. Guessing
    /// from a process list or an undocumented registry blob only recognises the
    /// tools someone thought to enumerate; reading the LUT back catches
    /// anything that writes it, including a vendor control panel nobody
    /// anticipated.
    /// </remarks>
    GammaVerification Verify();
}
