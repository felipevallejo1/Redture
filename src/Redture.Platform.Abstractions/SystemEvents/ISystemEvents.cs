namespace Redture.Platform.Abstractions.SystemEvents;

/// <summary>
/// OS-level notifications Redture has to react to.
/// </summary>
/// <remarks>
/// Display changes and the panic hotkey are exposed by one interface because on
/// Windows they arrive through the same mechanism: a single hidden top-level
/// window receiving <c>WM_DISPLAYCHANGE</c> and <c>WM_HOTKEY</c>. Splitting them
/// would mean two windows and two message paths for no benefit.
/// </remarks>
public interface ISystemEvents : IDisposable
{
    /// <summary>
    /// Raised when displays are attached, detached, resized or rearranged.
    /// Fires in bursts — subscribers must debounce.
    /// </summary>
    event EventHandler? DisplaysChanged;

    /// <summary>
    /// Raised when the user presses the panic hotkey. The contract is absolute:
    /// whatever state the app is in, this must return the screen to normal.
    /// It is the escape hatch for a deeply dimmed screen.
    /// </summary>
    event EventHandler? PanicRequested;

    /// <summary>
    /// Human-readable form of the panic hotkey (for example
    /// <c>Ctrl + Alt + Shift + R</c>), or <see langword="null"/> when it could
    /// not be registered — another application may already own the combination.
    /// The UI must only advertise the shortcut when this is non-null, otherwise
    /// it promises an escape hatch that does not exist.
    /// </summary>
    string? PanicHotkeyDescription { get; }

    /// <summary>
    /// Starts listening. Must be called on the thread that owns the message
    /// loop, and only once.
    /// </summary>
    void Start();
}
