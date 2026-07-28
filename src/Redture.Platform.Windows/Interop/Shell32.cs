using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>
/// P/Invoke declarations for <c>shell32.dll</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class Shell32
{
    /// <summary>
    /// <c>QUNS_BUSY</c>: a full-screen application is running, or presentation
    /// settings are applied.
    /// </summary>
    /// <remarks>
    /// <b>Deliberately never acted on.</b> Borderless fullscreen reports this
    /// rather than <see cref="RunningFullScreenDirect3D"/>, which makes it look
    /// like the state worth watching — but Redture's own dimming overlay is a
    /// borderless window the size of the screen and produces it too. Treating
    /// it as fullscreen made the overlay hide itself, which cleared the state,
    /// which made it reappear: fifteen transitions in forty seconds when
    /// measured. Borderless fullscreen is detected by looking at the foreground
    /// window instead, which can tell whose window it is.
    /// </remarks>
    internal const int Busy = 2;

    /// <summary>
    /// <c>QUNS_RUNNING_D3D_FULL_SCREEN</c>: a Direct3D application owns the
    /// screen in exclusive mode. Increasingly rare, since borderless is now the
    /// default in most engines.
    /// </summary>
    internal const int RunningFullScreenDirect3D = 3;

    /// <summary><c>QUNS_PRESENTATION_MODE</c>: the user asked not to be interrupted.</summary>
    internal const int PresentationMode = 4;

    /// <summary><c>QUNS_APP</c>: a Store app is running full screen.</summary>
    internal const int StoreAppFullScreen = 7;

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
