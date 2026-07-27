namespace Redture.Platform.Abstractions.Gamma;

/// <summary>
/// What the display's colour lookup table actually contains, compared with what
/// Redture last wrote to it.
/// </summary>
public enum GammaVerification
{
    /// <summary>The table could not be read back, so nothing can be concluded.</summary>
    Unknown = 0,

    /// <summary>The driver holds exactly the ramp Redture wrote.</summary>
    Matches = 1,

    /// <summary>
    /// The driver holds something else. Another application is writing the same
    /// table, and whoever writes last wins — the ping-pong that makes two
    /// colour tools flicker against each other.
    /// </summary>
    Foreign = 2,
}
