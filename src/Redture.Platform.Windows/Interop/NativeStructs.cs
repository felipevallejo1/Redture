using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>Win32 <c>POINTL</c> / <c>POINT</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
[SupportedOSPlatform("windows")]
internal struct PointL
{
    public int X;
    public int Y;
}

/// <summary>
/// Win32 <c>DISPLAY_DEVICEW</c>. Returned by <see cref="User32.EnumDisplayDevicesW"/>
/// both for adapters (when the device name is null) and for the monitor attached
/// to an adapter (when the adapter's device name is passed in).
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
[SupportedOSPlatform("windows")]
internal struct DisplayDevice
{
    /// <summary>Must be set to <c>sizeof(DISPLAY_DEVICEW)</c> before the call.</summary>
    public uint cb;

    /// <summary>Adapter output path, e.g. <c>\\.\DISPLAY1</c>.</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;

    /// <summary>Adapter model, or the monitor model when querying a monitor.</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;

    public uint StateFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceID;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;
}

/// <summary>
/// Win32 <c>DEVMODEW</c>, laid out field by field to match the native header.
/// </summary>
/// <remarks>
/// Only the display-related members are ever read here (<see cref="dmPosition"/>,
/// <see cref="dmPelsWidth"/>, <see cref="dmPelsHeight"/>), but the whole struct
/// must still be declared so its size — and therefore <c>dmSize</c> — matches
/// what the OS expects. The union at offset 0x5C is declared in its display
/// form (POINTL + two DWORDs); the printer form occupies the same 16 bytes.
/// </remarks>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
[SupportedOSPlatform("windows")]
internal struct DevMode
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string dmDeviceName;

    public ushort dmSpecVersion;
    public ushort dmDriverVersion;

    /// <summary>Size of this struct; must be filled in before the call.</summary>
    public ushort dmSize;

    public ushort dmDriverExtra;
    public uint dmFields;

    // --- Union, display form ---
    public PointL dmPosition;
    public uint dmDisplayOrientation;
    public uint dmDisplayFixedOutput;
    // --- End of union ---

    public short dmColor;
    public short dmDuplex;
    public short dmYResolution;
    public short dmTTOption;
    public short dmCollate;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string dmFormName;

    public ushort dmLogPixels;
    public uint dmBitsPerPel;
    public uint dmPelsWidth;
    public uint dmPelsHeight;
    public uint dmDisplayFlags;
    public uint dmDisplayFrequency;
    public uint dmICMMethod;
    public uint dmICMIntent;
    public uint dmMediaType;
    public uint dmDitherType;
    public uint dmReserved1;
    public uint dmReserved2;
    public uint dmPanningWidth;
    public uint dmPanningHeight;
}
