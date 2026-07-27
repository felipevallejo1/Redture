using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Linux.Interop;

/// <summary>
/// <c>XRRScreenResources</c>: the CRTCs and outputs the server knows about.
/// </summary>
/// <remarks>
/// Only the counts and the two array pointers are read. Every member is either
/// a machine word or an <c>int</c> followed by a pointer, so the natural
/// alignment C# applies matches what the C compiler produced.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("linux")]
internal struct XRRScreenResources
{
    public nuint Timestamp;
    public nuint ConfigTimestamp;
    public int CrtcCount;
    public nint Crtcs;
    public int OutputCount;
    public nint Outputs;
    public int ModeCount;
    public nint Modes;
}

/// <summary>
/// <c>XRRCrtcInfo</c>: where one CRTC is scanning out, and how big.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("linux")]
internal struct XRRCrtcInfo
{
    public nuint Timestamp;
    public int X;
    public int Y;
    public uint Width;
    public uint Height;
    public nuint Mode;

    /// <summary><c>Rotation</c>, a <c>unsigned short</c> in the C header.</summary>
    public ushort Rotation;

    public int OutputCount;
    public nint Outputs;
    public ushort Rotations;
    public int PossibleCount;
    public nint Possible;
}

/// <summary>
/// <c>XRROutputInfo</c>: a physical connector and whether anything is plugged in.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("linux")]
internal struct XRROutputInfo
{
    public nuint Timestamp;
    public nuint Crtc;

    /// <summary>Pointer to a non-null-terminated name of <see cref="NameLength"/> bytes.</summary>
    public nint Name;

    public int NameLength;
    public nuint MillimetreWidth;
    public nuint MillimetreHeight;

    /// <summary>0 = connected, 1 = disconnected, 2 = unknown.</summary>
    public ushort Connection;

    public ushort SubpixelOrder;
    public int CrtcCount;
    public nint Crtcs;
    public int CloneCount;
    public nint Clones;
    public int ModeCount;
    public int PreferredCount;
    public nint Modes;
}

/// <summary>
/// <c>XRRCrtcGamma</c>: three ramps of <see cref="Size"/> entries each.
/// </summary>
/// <remarks>
/// Never allocated here. <c>XRRAllocGamma</c> builds it and lays the three
/// arrays out immediately after the header, so letting the library own the
/// allocation removes any chance of getting that arrangement wrong.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("linux")]
internal struct XRRCrtcGamma
{
    public int Size;
    public nint Red;
    public nint Green;
    public nint Blue;
}

/// <summary>
/// P/Invoke declarations for <c>libXrandr</c>, which owns both display topology
/// and per-CRTC colour lookup tables on X11.
/// </summary>
[SupportedOSPlatform("linux")]
internal static partial class XRandr
{
    private const string Library = "libXrandr.so.2";

    /// <summary><c>RR_Connected</c>.</summary>
    internal const ushort Connected = 0;

    /// <summary>
    /// Reads the current topology without asking the hardware to re-probe.
    /// The re-probing variant takes hundreds of milliseconds and wakes displays
    /// up, which is not something a colour tool should do on a timer.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial nint XRRGetScreenResourcesCurrent(nint display, nuint window);

    [LibraryImport(Library)]
    internal static partial void XRRFreeScreenResources(nint resources);

    [LibraryImport(Library)]
    internal static partial nint XRRGetOutputInfo(nint display, nint resources, nuint output);

    [LibraryImport(Library)]
    internal static partial void XRRFreeOutputInfo(nint outputInfo);

    [LibraryImport(Library)]
    internal static partial nint XRRGetCrtcInfo(nint display, nint resources, nuint crtc);

    [LibraryImport(Library)]
    internal static partial void XRRFreeCrtcInfo(nint crtcInfo);

    /// <summary>
    /// Entries per channel this CRTC's lookup table holds. Commonly 256, 1024
    /// or 2048 depending on the driver, and a ramp of any other size is
    /// rejected.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial int XRRGetCrtcGammaSize(nint display, nuint crtc);

    [LibraryImport(Library)]
    internal static partial nint XRRAllocGamma(int size);

    [LibraryImport(Library)]
    internal static partial void XRRFreeGamma(nint gamma);

    [LibraryImport(Library)]
    internal static partial void XRRSetCrtcGamma(nint display, nuint crtc, nint gamma);

    [LibraryImport(Library)]
    internal static partial nint XRRGetCrtcGamma(nint display, nuint crtc);
}
