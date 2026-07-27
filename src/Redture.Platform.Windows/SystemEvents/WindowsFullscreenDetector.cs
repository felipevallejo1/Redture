using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.SystemEvents;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.SystemEvents;

/// <inheritdoc cref="IFullscreenDetector" />
/// <remarks>
/// <para>
/// Driven by a foreground-change hook rather than a timer. Entering or leaving
/// a fullscreen application changes which window is in front, so the one event
/// that matters is already being broadcast — polling would burn cycles for
/// weeks to catch a transition that announces itself.
/// </para>
/// <para>
/// The hook only wakes the detector up; the actual question is put to the shell
/// via <c>SHQueryUserNotificationState</c>. Measuring a window's rectangle
/// against the monitor's is the obvious alternative and a poor one — it counts
/// every maximised borderless window, and every wallpaper host, as fullscreen.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsFullscreenDetector : IFullscreenDetector
{
    private readonly ILogger<WindowsFullscreenDetector> _logger;

    /// <summary>Rooted for the lifetime of the hook; see <see cref="WinEventProcedure"/>.</summary>
    private readonly WinEventProcedure _callback;

    private nint _hook;
    private bool _started;
    private bool _disposed;

    public WindowsFullscreenDetector(ILogger<WindowsFullscreenDetector> logger)
    {
        _logger = logger;
        _callback = OnForegroundChanged;
    }

    public bool IsFullscreenActive { get; private set; }

    public event EventHandler<bool>? FullscreenStateChanged;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_started)
        {
            return;
        }

        _started = true;

        _hook = User32.SetWinEventHook(
            User32.EventSystemForeground,
            User32.EventSystemForeground,
            moduleHandle: 0,
            _callback,
            processId: 0,
            threadId: 0,
            User32.WinEventOutOfContext);

        if (_hook == 0)
        {
            _logger.LogWarning(
                "Could not install the foreground hook; the overlay will not stand down for fullscreen applications.");
            return;
        }

        // Establish the starting answer: Redture may well have been launched
        // while something was already fullscreen.
        Evaluate();
        _logger.LogInformation("Fullscreen detection active (currently {State}).", IsFullscreenActive);
    }

    private void OnForegroundChanged(
        nint hookHandle,
        uint eventType,
        nint windowHandle,
        int objectId,
        int childId,
        uint eventThreadId,
        uint eventTimeMilliseconds)
    {
        try
        {
            Evaluate();
        }
        catch (Exception ex)
        {
            // Called from native code: an exception escaping here would tear the
            // process down without a managed stack trace.
            _logger.LogError(ex, "Failed to evaluate the fullscreen state.");
        }
    }

    private void Evaluate()
    {
        if (Shell32.SHQueryUserNotificationState(out int state) != 0)
        {
            return;
        }

        bool fullscreen = state is Shell32.RunningFullScreenDirect3D or Shell32.PresentationMode;

        if (fullscreen == IsFullscreenActive)
        {
            return;
        }

        IsFullscreenActive = fullscreen;
        _logger.LogInformation(
            fullscreen
                ? "An application has taken over the screen; standing down."
                : "The desktop is back; restoring display corrections.");

        FullscreenStateChanged?.Invoke(this, fullscreen);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hook != 0)
        {
            User32.UnhookWinEvent(_hook);
            _hook = 0;
        }
    }
}
