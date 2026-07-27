using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Linux.Interop;

/// <summary>
/// P/Invoke declarations for <c>libX11</c>.
/// </summary>
/// <remarks>
/// The library is loaded by soname rather than by plain name: distributions
/// ship <c>libX11.so.6</c> and only install the unversioned <c>libX11.so</c>
/// symlink with the development package, which end users do not have.
/// </remarks>
[SupportedOSPlatform("linux")]
internal static partial class Xlib
{
    private const string Library = "libX11.so.6";

    /// <summary>
    /// Opens a connection to an X server. Null asks for the one named by
    /// <c>$DISPLAY</c>, which is what every normal session provides.
    /// </summary>
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint XOpenDisplay(string? displayName);

    [LibraryImport(Library)]
    internal static partial int XCloseDisplay(nint display);

    /// <summary>
    /// Declares that Xlib will be called from more than one thread. Must be the
    /// first Xlib call the process makes; afterwards the library takes its own
    /// locks. Redture also serialises its own calls, but this covers Avalonia's
    /// X11 backend running on the UI thread at the same time.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial int XInitThreads();

    [LibraryImport(Library)]
    internal static partial nint XDefaultRootWindow(nint display);

    [LibraryImport(Library)]
    internal static partial int XFree(nint data);

    /// <summary>Flushes buffered requests to the server.</summary>
    [LibraryImport(Library)]
    internal static partial int XFlush(nint display);

    /// <summary>Waits for every outstanding request to complete.</summary>
    [LibraryImport(Library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool XSync(nint display, [MarshalAs(UnmanagedType.Bool)] bool discard);
}
