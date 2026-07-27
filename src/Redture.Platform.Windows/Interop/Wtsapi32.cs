using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>
/// P/Invoke declarations for <c>wtsapi32.dll</c> — session change notifications.
/// </summary>
/// <remarks>
/// Needed because Windows drops the gamma ramp when the session locks or a
/// secure desktop takes over (a UAC prompt, the lock screen, fast user
/// switching). Without a notification on the way back, the display silently
/// loses its colour correction and only gets it again on the next slider move.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static partial class Wtsapi32
{
    internal const uint WmWtsSessionChange = 0x02B1;

    /// <summary>Only tell us about the session this process belongs to.</summary>
    internal const uint NotifyForThisSession = 0x00000000;

    /// <summary><c>WTS_SESSION_UNLOCK</c>.</summary>
    internal const int SessionUnlock = 0x8;

    /// <summary><c>WTS_SESSION_LOGON</c>.</summary>
    internal const int SessionLogon = 0x5;

    [LibraryImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSRegisterSessionNotification(nint hWnd, uint dwFlags);

    [LibraryImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSUnRegisterSessionNotification(nint hWnd);
}
