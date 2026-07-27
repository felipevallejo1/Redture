namespace Redture.Platform.Abstractions.SystemEvents;

/// <summary>
/// Reports when an application has taken over the whole screen.
/// </summary>
/// <remarks>
/// <para>
/// Exclusive fullscreen bypasses the desktop compositor entirely, which puts it
/// underneath nothing: a topmost overlay simply does not appear over it, and
/// the application usually claims the gamma ramp as well. Redture cannot win
/// that argument and should not try.
/// </para>
/// <para>
/// So it stands down instead. Fighting for z-order against a game produces
/// exactly the flicker this project exists to avoid, and the overlay would not
/// be visible for the trouble. What matters is noticing when the game exits, so
/// the correction can be put back.
/// </para>
/// </remarks>
public interface IFullscreenDetector : IDisposable
{
    /// <summary>Whether an application currently owns the whole screen.</summary>
    bool IsFullscreenActive { get; }

    /// <summary>Raised when that answer changes.</summary>
    event EventHandler<bool>? FullscreenStateChanged;

    /// <summary>
    /// Starts watching. Must be called on the thread that owns the message
    /// loop, since the notifications arrive through it.
    /// </summary>
    void Start();
}
