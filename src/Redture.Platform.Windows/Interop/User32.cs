using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>
/// Window procedure callback. Instances handed to <c>RegisterClassEx</c> must be
/// kept alive by managed code for as long as the class is registered — the
/// runtime does not root a delegate just because a function pointer to it was
/// passed to native code.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
internal delegate nint WindowProcedure(nint hWnd, uint message, nint wParam, nint lParam);

/// <summary>
/// P/Invoke declarations for <c>user32.dll</c>.
/// </summary>
/// <remarks>
/// <c>DllImport</c> rather than the newer <c>LibraryImport</c> source generator:
/// several of these take structs containing fixed-length string buffers
/// (<c>ByValTStr</c>), which the generator does not marshal. The generator is
/// used for the blittable signatures instead — see <see cref="Shcore"/>.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class User32
{
    // --- Display enumeration ------------------------------------------------

    /// <summary>The device is part of the desktop (i.e. actually in use).</summary>
    internal const uint DisplayDeviceAttachedToDesktop = 0x00000001;

    /// <summary>The device is the primary display.</summary>
    internal const uint DisplayDevicePrimaryDevice = 0x00000004;

    /// <summary>Ask <see cref="EnumDisplaySettingsW"/> for the mode in use right now.</summary>
    internal const int EnumCurrentSettings = -1;

    /// <summary>Return the nearest monitor when a point falls outside every display.</summary>
    internal const uint MonitorDefaultToNearest = 0x00000002;

    // --- Window styles ------------------------------------------------------

    /// <summary>A window with no frame, no title bar and no border.</summary>
    internal const uint WsPopup = 0x80000000;

    /// <summary>Enables per-window alpha through <see cref="SetLayeredWindowAttributes"/>.</summary>
    internal const uint WsExLayered = 0x00080000;

    /// <summary>Hit-testing passes straight through: the window is click-through.</summary>
    internal const uint WsExTransparent = 0x00000020;

    /// <summary>Sits above non-topmost windows.</summary>
    internal const uint WsExTopmost = 0x00000008;

    /// <summary>Keeps the window out of the taskbar and out of Alt-Tab.</summary>
    internal const uint WsExToolWindow = 0x00000080;

    /// <summary>The window never takes focus, even when clicked.</summary>
    internal const uint WsExNoActivate = 0x08000000;

    // --- SetWindowPos -------------------------------------------------------

    internal static readonly nint HwndTopmost = -1;

    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpShowWindow = 0x0040;

    internal const int SwHide = 0;

    // --- Layered windows ----------------------------------------------------

    /// <summary>Use the alpha argument of <see cref="SetLayeredWindowAttributes"/>.</summary>
    internal const uint LwaAlpha = 0x00000002;

    /// <summary>
    /// <c>WDA_EXCLUDEFROMCAPTURE</c>: the window is rendered to the screen but
    /// omitted from screen captures and screen sharing. Windows 10 2004+.
    /// </summary>
    internal const uint WdaExcludeFromCapture = 0x00000011;

    // --- Messages -----------------------------------------------------------

    internal const uint WmDisplayChange = 0x007E;
    internal const uint WmDpiChanged = 0x02E0;
    internal const uint WmHotkey = 0x0312;

    // --- Hotkey modifiers ---------------------------------------------------

    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;

    /// <summary>Do not repeat while the keys are held down.</summary>
    internal const uint ModNoRepeat = 0x4000;

    /// <summary>Virtual key code for 'R'.</summary>
    internal const uint VkR = 0x52;

    /// <summary><c>ERROR_CLASS_ALREADY_EXISTS</c>, harmless on re-registration.</summary>
    internal const int ErrorClassAlreadyExists = 1410;

    // --- Display enumeration ------------------------------------------------

    /// <summary>
    /// Enumerates display adapters (<paramref name="lpDevice"/> null) or the
    /// monitors attached to one adapter (<paramref name="lpDevice"/> set to the
    /// adapter's device name).
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayDevicesW(
        string? lpDevice,
        uint iDevNum,
        ref DisplayDevice lpDisplayDevice,
        uint dwFlags);

    /// <summary>
    /// Reads the graphics mode of a display adapter, which is how we obtain the
    /// display's position and resolution without an enumeration callback.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplaySettingsW(
        string lpszDeviceName,
        int iModeNum,
        ref DevMode lpDevMode);

    /// <summary>Resolves the monitor handle containing a given point.</summary>
    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(PointL pt, uint dwFlags);

    // --- Windows ------------------------------------------------------------

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassExW(ref WindowClassEx lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterClassW(string lpClassName, nint hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string? lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint DefWindowProcW(nint hWnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hWnd, int nCmdShow);

    // --- Layered windows and capture exclusion ------------------------------

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);

    // --- Global hotkeys -----------------------------------------------------

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint hWnd, int id);

    // --- Foreground window inspection ---------------------------------------

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassNameW(nint hWnd, [Out] char[] className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hWnd, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(nint hWnd);

    /// <summary>Window style bits; pass <see cref="GwlStyle"/>.</summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static extern nint GetWindowLongPtrW(nint hWnd, int index);

    /// <summary>Index of the window style word, for <see cref="GetWindowLongPtrW"/>.</summary>
    internal const int GwlStyle = -16;

    /// <summary>A window with a title bar. Fullscreen windows do not have one.</summary>
    internal const long WsCaption = 0x00C0_0000L;

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromWindow(nint hWnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

    // --- Accessibility event hooks ------------------------------------------

    /// <summary><c>EVENT_SYSTEM_FOREGROUND</c>.</summary>
    internal const uint EventSystemForeground = 0x0003;

    /// <summary>
    /// <c>WINEVENT_OUTOFCONTEXT</c>: deliver events by posting to our own
    /// thread rather than injecting a DLL into every process on the desktop.
    /// </summary>
    internal const uint WinEventOutOfContext = 0x0000;

    [DllImport("user32.dll")]
    internal static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint moduleHandle,
        WinEventProcedure callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hookHandle);
}

/// <summary>
/// Callback for <see cref="User32.SetWinEventHook"/>. Like a window procedure,
/// the delegate must be rooted for as long as the hook is installed.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate void WinEventProcedure(
    nint hookHandle,
    uint eventType,
    nint windowHandle,
    int objectId,
    int childId,
    uint eventThreadId,
    uint eventTimeMilliseconds);
