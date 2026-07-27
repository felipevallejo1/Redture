namespace Redture.Platform.Abstractions.Gamma;

/// <summary>
/// Whether the OS restricts how far a gamma ramp may deviate from linear.
/// </summary>
public enum GammaRangeState
{
    /// <summary>The platform has no such restriction.</summary>
    NotApplicable = 0,

    /// <summary>Could not be determined.</summary>
    Unknown = 1,

    /// <summary>
    /// Restricted. Windows validates every ramp against a narrow band around
    /// linear and refuses anything outside it, which is what strongly warm
    /// settings run into.
    /// </summary>
    Restricted = 2,

    /// <summary>The full range has been unlocked and is in effect.</summary>
    Unlocked = 3,

    /// <summary>
    /// Unlocked in the registry, but the machine has not been signed out since.
    /// The setting is read once per session, so it does nothing until then.
    /// </summary>
    UnlockedPendingSignOut = 4,
}
