using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.SystemEvents;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.SystemEvents;

/// <inheritdoc cref="IFullscreenDetector" />
/// <remarks>
/// <para>
/// Decides by inspecting the foreground window: which process owns it, what
/// class it is, and whether its rectangle covers the monitor it sits on.
/// </para>
/// <para>
/// The obvious alternative, <c>SHQueryUserNotificationState</c>, cannot be used
/// on its own here and the reason is worth recording. Its
/// <c>QUNS_RUNNING_D3D_FULL_SCREEN</c> state only covers exclusive-mode
/// Direct3D, which almost nothing uses any more; borderless fullscreen reports
/// <c>QUNS_BUSY</c> instead. But <c>QUNS_BUSY</c> is also what Redture's own
/// dimming overlay produces — it is, after all, a borderless window the size of
/// the screen. Acting on it made the overlay hide itself, which cleared the
/// state, which made it show itself again: measured at fifteen transitions in
/// forty seconds. Precisely the flicker this project exists to avoid.
/// </para>
/// <para>
/// The foreground-window test has the property that matters: it can tell whose
/// window it is looking at, and ignore our own. The shell query is still
/// consulted for the two states our overlay cannot possibly cause.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsFullscreenDetector : IFullscreenDetector
{
    /// <summary>
    /// Backstop interval. The foreground hook catches alt-tabbing into a game,
    /// but a window that goes fullscreen in place — a browser on F11, a video
    /// player expanding — never changes which window is in front, so no event
    /// is raised at all.
    /// </summary>
    /// <remarks>
    /// The one timer in the application that runs regardless of state, kept
    /// because the alternative is a feature that works for some ways of going
    /// fullscreen and not others. Two local calls every two seconds.
    /// </remarks>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Windows that are always the size of the screen and never mean fullscreen:
    /// the desktop itself, its wallpaper host, and the taskbar.
    /// </summary>
    private static readonly string[] ShellClassNames =
    [
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
    ];

    private readonly ILogger<WindowsFullscreenDetector> _logger;
    private readonly WinEventProcedure _callback;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;

    private nint _hook;
    private Task? _poll;
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
            _logger.LogWarning("Could not install the foreground hook; falling back to polling alone.");
        }

        Evaluate();
        _poll = Task.Run(PollAsync);

        _logger.LogInformation("Fullscreen detection active (currently {State}).", IsFullscreenActive);
    }

    private async Task PollAsync()
    {
        try
        {
            using PeriodicTimer timer = new(PollInterval);

            while (await timer.WaitForNextTickAsync(_shutdown.Token).ConfigureAwait(false))
            {
                Evaluate();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The fullscreen poll stopped unexpectedly.");
        }
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
        bool fullscreen = ForegroundWindowCoversAMonitor() || ShellReportsExclusiveFullscreen();

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

    /// <summary>
    /// Whether the window in front belongs to somebody else and fills the
    /// monitor it is on.
    /// </summary>
    private bool ForegroundWindowCoversAMonitor()
    {
        nint foreground = User32.GetForegroundWindow();
        if (foreground == 0)
        {
            return false;
        }

        // The check that makes this approach work at all: Redture's own overlay
        // is a borderless window covering the screen, and reacting to it would
        // make the overlay switch itself on and off forever.
        User32.GetWindowThreadProcessId(foreground, out uint processId);
        if (processId == _ownProcessId)
        {
            return false;
        }

        if (IsShellWindow(foreground))
        {
            return false;
        }

        if (!User32.GetWindowRect(foreground, out Rect window))
        {
            return false;
        }

        nint monitor = User32.MonitorFromWindow(foreground, User32.MonitorDefaultToNearest);
        if (monitor == 0)
        {
            return false;
        }

        MonitorInfo info = new() { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>() };
        if (!User32.GetMonitorInfoW(monitor, ref info))
        {
            return false;
        }

        // Covers rather than equals: a fullscreen window is sometimes a pixel
        // larger than the monitor, and comparing for equality misses those.
        return window.Left <= info.Monitor.Left
            && window.Top <= info.Monitor.Top
            && window.Right >= info.Monitor.Right
            && window.Bottom >= info.Monitor.Bottom;
    }

    private static bool IsShellWindow(nint window)
    {
        char[] buffer = new char[64];
        int length = User32.GetClassNameW(window, buffer, buffer.Length);

        if (length <= 0)
        {
            return false;
        }

        string className = new(buffer, 0, length);
        return Array.Exists(ShellClassNames, name => string.Equals(name, className, StringComparison.Ordinal));
    }

    /// <summary>
    /// The two shell states Redture's own overlay cannot produce: exclusive
    /// Direct3D, and the user explicitly turning on presentation mode.
    /// </summary>
    /// <remarks>
    /// <c>QUNS_BUSY</c> is deliberately not consulted. It is the state a
    /// borderless fullscreen window produces, and our overlay is one.
    /// </remarks>
    private static bool ShellReportsExclusiveFullscreen() =>
        Shell32.SHQueryUserNotificationState(out int state) == 0
        && state is Shell32.RunningFullScreenDirect3D or Shell32.PresentationMode;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();

        if (_hook != 0)
        {
            User32.UnhookWinEvent(_hook);
            _hook = 0;
        }

        _shutdown.Dispose();
    }
}
