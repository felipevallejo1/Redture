using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>
/// P/Invoke declarations for <c>shell32.dll</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class Shell32
{
    /// <summary><c>QUNS_RUNNING_D3D_FULL_SCREEN</c>: a Direct3D application owns the screen.</summary>
    internal const int RunningFullScreenDirect3D = 3;

    /// <summary><c>QUNS_PRESENTATION_MODE</c>: the user asked not to be interrupted.</summary>
    internal const int PresentationMode = 4;

    /// <summary>
    /// Asks the shell whether it is a good moment to show a notification.
    /// </summary>
    /// <remarks>
    /// Used here for the question it answers as a side effect: whether an
    /// application has taken over the screen. That is a far more reliable test
    /// than comparing a window's rectangle against the monitor's, which
    /// misidentifies every maximised borderless window as fullscreen.
    /// </remarks>
    [LibraryImport("shell32.dll")]
    internal static partial int SHQueryUserNotificationState(out int state);
}
