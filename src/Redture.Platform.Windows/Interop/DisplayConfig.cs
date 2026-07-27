using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>Win32 <c>LUID</c>. Alignment 4, size 8.</summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("windows")]
internal struct Luid
{
    public uint LowPart;
    public int HighPart;
}

/// <summary>Win32 <c>DISPLAYCONFIG_PATH_SOURCE_INFO</c>. Size 20.</summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("windows")]
internal struct DisplayConfigPathSourceInfo
{
    public Luid AdapterId;
    public uint Id;

    /// <summary>Union of <c>modeInfoIdx</c> and a pair of 16-bit indices.</summary>
    public uint ModeInfoIdx;

    /// <summary>
    /// Easy to miss, and expensive to miss: leaving this field out makes the
    /// struct 16 bytes instead of 20, which shifts every following member of
    /// the path and has the OS write past the end of the array it was given.
    /// </summary>
    public uint StatusFlags;
}

/// <summary>Win32 <c>DISPLAYCONFIG_PATH_TARGET_INFO</c>. Size 48.</summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("windows")]
internal struct DisplayConfigPathTargetInfo
{
    public Luid AdapterId;
    public uint Id;
    public uint ModeInfoIdx;
    public uint OutputTechnology;
    public uint Rotation;
    public uint Scaling;
    public uint RefreshRateNumerator;
    public uint RefreshRateDenominator;
    public uint ScanLineOrdering;
    public int TargetAvailable;
    public uint StatusFlags;
}

/// <summary>
/// Win32 <c>DISPLAYCONFIG_PATH_INFO</c>. Every member is 4-byte aligned, so the
/// size is the plain sum: 20 + 48 + 4 = 72.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("windows")]
internal struct DisplayConfigPathInfo
{
    public DisplayConfigPathSourceInfo SourceInfo;
    public DisplayConfigPathTargetInfo TargetInfo;
    public uint Flags;
}

/// <summary>
/// Win32 <c>DISPLAYCONFIG_MODE_INFO</c>. Size 64.
/// </summary>
/// <remarks>
/// The 48-byte union at the end is declared as opaque 64-bit words. Redture
/// never reads it — the array only exists because <c>QueryDisplayConfig</c>
/// insists on filling one — and spelling out three variants of a union that is
/// never inspected would be three more chances to get a layout wrong.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("windows")]
internal struct DisplayConfigModeInfo
{
    public uint InfoType;
    public uint Id;
    public Luid AdapterId;

    public ulong Union0;
    public ulong Union1;
    public ulong Union2;
    public ulong Union3;
    public ulong Union4;
    public ulong Union5;
}

/// <summary>Win32 <c>DISPLAYCONFIG_DEVICE_INFO_HEADER</c>. Size 20.</summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("windows")]
internal struct DisplayConfigDeviceInfoHeader
{
    public uint Type;

    /// <summary>Size of the whole request packet, not of this header.</summary>
    public uint Size;

    public Luid AdapterId;
    public uint Id;
}

/// <summary>Win32 <c>DISPLAYCONFIG_SOURCE_DEVICE_NAME</c>. Size 84.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
[SupportedOSPlatform("windows")]
internal struct DisplayConfigSourceDeviceName
{
    public DisplayConfigDeviceInfoHeader Header;

    /// <summary>GDI name, e.g. <c>\\.\DISPLAY1</c> — the key everything else is
    /// tracked by.</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string ViewGdiDeviceName;
}

/// <summary>Win32 <c>DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO</c>. Size 32.</summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("windows")]
internal struct DisplayConfigGetAdvancedColorInfo
{
    public DisplayConfigDeviceInfoHeader Header;

    /// <summary>
    /// Bitfield: bit 0 advancedColorSupported, bit 1 advancedColorEnabled,
    /// bit 2 wideColorEnforced, bit 3 advancedColorForceDisabled.
    /// </summary>
    public uint Value;

    public uint ColorEncoding;
    public uint BitsPerColorChannel;

    /// <summary>Whether the display is presently running in HDR mode.</summary>
    public readonly bool AdvancedColorEnabled => (Value & 0x2) != 0;

    /// <summary>Whether the display is capable of HDR at all.</summary>
    public readonly bool AdvancedColorSupported => (Value & 0x1) != 0;
}

/// <summary>
/// Win32 <c>DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2</c>. Size 28.
/// </summary>
/// <remarks>
/// The documented replacement for <see cref="DisplayConfigGetAdvancedColorInfo"/>,
/// separating the questions properly: whether the display can do HDR, whether
/// the user turned it on, and which mode it is in right now.
/// <para>
/// Unverified. On Windows 11 build 10.0.26200 this query returns
/// <c>ERROR_INVALID_PARAMETER</c> while the older one answers correctly, so
/// this layout has never been exercised against a successful call. It is kept
/// only as a fallback for builds that drop the original.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("windows")]
internal struct DisplayConfigGetAdvancedColorInfo2
{
    public DisplayConfigDeviceInfoHeader Header;

    /// <summary>
    /// Bitfield: bit 0 advancedColorSupported, bit 1 advancedColorActive,
    /// bit 3 advancedColorLimitedByPolicy, bit 4 highDynamicRangeSupported,
    /// bit 5 highDynamicRangeUserEnabled, bit 6 wideColorSupported,
    /// bit 7 wideColorUserEnabled.
    /// </summary>
    public uint Value;

    /// <summary><c>DISPLAYCONFIG_ADVANCED_COLOR_MODE</c>: 0 SDR, 1 WCG, 2 HDR.</summary>
    public uint ActiveColorMode;

    /// <summary>
    /// True only when the display is presently in HDR mode. Wide colour gamut
    /// is deliberately not counted: it does not stop a gamma ramp from working.
    /// </summary>
    public readonly bool IsHdrActive => ActiveColorMode == 2;

    /// <summary>Whether the panel is capable of HDR at all.</summary>
    public readonly bool IsHdrSupported => (Value & 0x10) != 0;
}

/// <summary>
/// P/Invoke declarations for the Windows display configuration API.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class DisplayConfig
{
    /// <summary><c>QDC_ONLY_ACTIVE_PATHS</c>.</summary>
    internal const uint OnlyActivePaths = 0x00000002;

    internal const uint DeviceInfoGetSourceName = 1;

    /// <summary>Deprecated in Windows 11 24H2; kept as a fallback for older builds.</summary>
    internal const uint DeviceInfoGetAdvancedColorInfo = 9;

    /// <summary><c>DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2</c>.</summary>
    internal const uint DeviceInfoGetAdvancedColorInfo2 = 13;

    internal const int ErrorSuccess = 0;

    // Expected marshalled sizes, checked at runtime before any of these
    // functions is called.
    //
    // Note what this check can and cannot do. It compares the C# declaration
    // against a number written here, so it only catches a declaration drifting
    // from its documented size -- it cannot catch both being wrong together,
    // which is exactly what happened during development when a field was
    // omitted from the source-info struct. The semantic cross-check in the
    // detector, that a path's source and target report the same adapter, is
    // what actually catches a bad layout.
    internal const int PathInfoSize = 72;
    internal const int ModeInfoSize = 64;
    internal const int SourceDeviceNameSize = 84;
    internal const int AdvancedColorInfoSize = 32;
    internal const int AdvancedColorInfo2Size = 28;

    [DllImport("user32.dll")]
    internal static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    internal static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        nint currentTopologyId);

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName request);

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigGetAdvancedColorInfo request);

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigGetAdvancedColorInfo2 request);
}
