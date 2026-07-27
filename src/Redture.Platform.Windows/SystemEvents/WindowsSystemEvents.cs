using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.SystemEvents;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.SystemEvents;

/// <inheritdoc cref="ISystemEvents" />
/// <remarks>
/// <para>
/// Backed by one hidden top-level window. It is deliberately <em>not</em> a
/// message-only window (<c>HWND_MESSAGE</c>): those do not receive broadcast
/// messages, and <c>WM_DISPLAYCHANGE</c> is a broadcast. So the window is a
/// normal pop-up that is simply never shown — zero pixels, but still on the
/// broadcast list.
/// </para>
/// <para>
/// The window is created on the caller's thread, which must be the UI thread:
/// its messages are then pumped by the application's existing message loop, so
/// no extra thread is needed.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsSystemEvents : ISystemEvents
{
    private const string ClassName = "RedtureMessageWindow";

    /// <summary>Arbitrary but stable identifier for the panic hotkey ('RT').</summary>
    private const int PanicHotkeyId = 0x5254;

    private readonly ILogger<WindowsSystemEvents> _logger;

    /// <summary>
    /// Rooted for the lifetime of the window: the function pointer handed to
    /// the window class must stay valid, and the GC does not know native code
    /// is holding it.
    /// </summary>
    private readonly WindowProcedure _procedure;

    private nint _handle;
    private bool _hotkeyRegistered;
    private bool _sessionNotificationsRegistered;
    private bool _started;
    private bool _disposed;

    public WindowsSystemEvents(ILogger<WindowsSystemEvents> logger)
    {
        _logger = logger;
        _procedure = OnMessage;
    }

    public event EventHandler? DisplaysChanged;

    public event EventHandler? SessionResumed;

    public event EventHandler? PanicRequested;

    public string? PanicHotkeyDescription => _hotkeyRegistered ? "Ctrl + Alt + Shift + R" : null;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_started)
        {
            return;
        }

        _started = true;

        RegisterWindowClass();

        _handle = User32.CreateWindowExW(
            User32.WsExToolWindow,
            ClassName,
            "Redture system events",
            User32.WsPopup,
            x: 0,
            y: 0,
            nWidth: 0,
            nHeight: 0,
            hWndParent: 0,
            hMenu: 0,
            NativeModule.Handle,
            lpParam: 0);

        if (_handle == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the Redture message window.");
        }

        RegisterPanicHotkey();
        RegisterSessionNotifications();

        _logger.LogInformation(
            "System event listener started. Panic hotkey: {Hotkey}.",
            PanicHotkeyDescription ?? "unavailable");
    }

    private void RegisterWindowClass()
    {
        WindowClassEx windowClass = new()
        {
            cbSize = (uint)Marshal.SizeOf<WindowClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_procedure),
            hInstance = NativeModule.Handle,
            lpszClassName = ClassName,
        };

        if (User32.RegisterClassExW(ref windowClass) != 0)
        {
            return;
        }

        int error = Marshal.GetLastWin32Error();
        if (error != User32.ErrorClassAlreadyExists)
        {
            throw new Win32Exception(error, "Could not register the Redture message window class.");
        }
    }

    /// <summary>
    /// Registers the escape hatch. Failure is not fatal — another app may own
    /// the combination — but it must be visible, because the UI stops promising
    /// the shortcut when it is not there.
    /// </summary>
    private void RegisterPanicHotkey()
    {
        _hotkeyRegistered = User32.RegisterHotKey(
            _handle,
            PanicHotkeyId,
            User32.ModControl | User32.ModAlt | User32.ModShift | User32.ModNoRepeat,
            User32.VkR);

        if (!_hotkeyRegistered)
        {
            _logger.LogWarning(
                "Could not register the panic hotkey (error {Error}); another application probably owns it.",
                Marshal.GetLastWin32Error());
        }
    }

    /// <summary>
    /// Subscribes to lock/unlock notifications. Not fatal if it fails: the app
    /// still works, it just will not repair the gamma ramp after a lock screen.
    /// </summary>
    private void RegisterSessionNotifications()
    {
        _sessionNotificationsRegistered = Wtsapi32.WTSRegisterSessionNotification(
            _handle,
            Wtsapi32.NotifyForThisSession);

        if (!_sessionNotificationsRegistered)
        {
            _logger.LogWarning(
                "Could not subscribe to session notifications (error {Error}); colour correction will not be re-applied automatically after a lock screen.",
                Marshal.GetLastWin32Error());
        }
    }

    /// <summary>
    /// Window procedure. Runs on the UI thread, called from native code — an
    /// exception escaping here would tear the process down without a managed
    /// stack trace, so everything is caught and logged.
    /// </summary>
    private nint OnMessage(nint hWnd, uint message, nint wParam, nint lParam)
    {
        try
        {
            switch (message)
            {
                case User32.WmDisplayChange:
                case User32.WmDpiChanged:
                    _logger.LogDebug("Display configuration changed (message 0x{Message:X4}).", message);
                    DisplaysChanged?.Invoke(this, EventArgs.Empty);
                    break;

                case Wtsapi32.WmWtsSessionChange when (int)wParam is Wtsapi32.SessionUnlock or Wtsapi32.SessionLogon:
                    _logger.LogDebug("Session resumed (event {Event}).", (int)wParam);
                    SessionResumed?.Invoke(this, EventArgs.Empty);
                    break;

                case User32.WmHotkey when (int)wParam == PanicHotkeyId:
                    _logger.LogInformation("Panic hotkey pressed; restoring the display to a neutral state.");
                    PanicRequested?.Invoke(this, EventArgs.Empty);
                    break;

                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing window message 0x{Message:X4}.", message);
        }

        return User32.DefWindowProcW(hWnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_handle == 0)
        {
            return;
        }

        if (_hotkeyRegistered)
        {
            User32.UnregisterHotKey(_handle, PanicHotkeyId);
            _hotkeyRegistered = false;
        }

        if (_sessionNotificationsRegistered)
        {
            Wtsapi32.WTSUnRegisterSessionNotification(_handle);
            _sessionNotificationsRegistered = false;
        }

        User32.DestroyWindow(_handle);
        _handle = 0;

        _logger.LogDebug("System event listener disposed.");
    }
}
