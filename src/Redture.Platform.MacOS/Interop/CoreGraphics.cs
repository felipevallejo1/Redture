using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.MacOS.Interop;

/// <summary>Core Graphics <c>CGPoint</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("macos")]
internal struct CGPoint
{
    public double X;
    public double Y;
}

/// <summary>Core Graphics <c>CGSize</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("macos")]
internal struct CGSize
{
    public double Width;
    public double Height;
}

/// <summary>
/// Core Graphics <c>CGRect</c>: four doubles, and nothing else.
/// </summary>
/// <remarks>
/// Worth noting how much simpler this is than the equivalents on the other two
/// platforms. Windows needed a 72-byte struct with a union and a field that was
/// easy to miss; XRandR needed four nested structs. Here the entire surface is
/// four doubles and a 32-bit display id, which is why this backend carries far
/// less layout risk than either of the others.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("macos")]
internal struct CGRect
{
    public CGPoint Origin;
    public CGSize Size;
}

/// <summary>
/// P/Invoke declarations for Core Graphics, which owns both display topology
/// and per-display colour lookup tables on macOS.
/// </summary>
/// <remarks>
/// <b>Unverified.</b> Written from the published API but never executed: there
/// was no Mac available while this was built, and macOS cannot be containerised
/// the way the Linux backend was. Every entry point below degrades to "not
/// supported" on any failure rather than throwing, so the worst outcome on a
/// real Mac is that colour temperature quietly does nothing and the diagnostics
/// tool says so.
/// </remarks>
[SupportedOSPlatform("macos")]
internal static partial class CoreGraphics
{
    private const string Library = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    /// <summary><c>kCGErrorSuccess</c>.</summary>
    internal const int Success = 0;

    /// <summary>Ceiling for one call to <see cref="CGGetActiveDisplayList"/>.</summary>
    internal const uint MaxDisplays = 16;

    [LibraryImport(Library)]
    internal static partial int CGGetActiveDisplayList(
        uint maxDisplays,
        [Out] uint[] activeDisplays,
        out uint displayCount);

    [LibraryImport(Library)]
    internal static partial CGRect CGDisplayBounds(uint display);

    /// <summary>
    /// Returns <c>boolean_t</c>, which is a 32-bit int rather than a byte.
    /// Declared as an int and compared explicitly, so the result does not
    /// depend on how a bool happens to be marshalled.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial int CGDisplayIsMain(uint display);

    [LibraryImport(Library)]
    internal static partial uint CGDisplayModelNumber(uint display);

    /// <summary>Entries the display's lookup table can hold.</summary>
    [LibraryImport(Library)]
    internal static partial uint CGDisplayGammaTableCapacity(uint display);

    /// <summary>
    /// Loads a lookup table. Values are floats in [0, 1] rather than the 16-bit
    /// integers Windows and X11 use.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial int CGSetDisplayTransferByTable(
        uint display,
        uint tableSize,
        [In] float[] redTable,
        [In] float[] greenTable,
        [In] float[] blueTable);

    [LibraryImport(Library)]
    internal static partial int CGGetDisplayTransferByTable(
        uint display,
        uint capacity,
        [Out] float[] redTable,
        [Out] float[] greenTable,
        [Out] float[] blueTable,
        out uint sampleCount);

    /// <summary>
    /// Restores every display's table to its colour-profile default.
    /// </summary>
    /// <remarks>
    /// Preferred over writing an identity ramp: it returns the display to the
    /// user's calibration profile rather than to a linear ramp, which on a
    /// calibrated Mac is not the same thing at all.
    /// </remarks>
    [LibraryImport(Library)]
    internal static partial void CGDisplayRestoreColorSyncSettings();
}
