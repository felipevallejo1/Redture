using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>Win32 <c>PHYSICAL_MONITOR</c>.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
[SupportedOSPlatform("windows")]
internal struct PhysicalMonitor
{
    /// <summary>
    /// Handle to the monitor's DDC/CI channel. Must be released through
    /// <see cref="Dxva2.DestroyPhysicalMonitors"/> — and released as the whole
    /// array it was obtained in, never one element at a time.
    /// </summary>
    public nint hPhysicalMonitor;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string szPhysicalMonitorDescription;
}

/// <summary>
/// P/Invoke declarations for <c>dxva2.dll</c> — the monitor configuration API,
/// which speaks DDC/CI over the video cable's I²C channel.
/// </summary>
/// <remarks>
/// Every call here is slow by normal standards: roughly 60 ms each on real
/// hardware, read or write, because it is a round trip to the monitor's
/// firmware. Nothing in this file may be called from the UI thread.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class Dxva2
{
    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint hMonitor, out uint numberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPhysicalMonitorsFromHMONITOR(
        nint hMonitor,
        uint physicalMonitorArraySize,
        [Out] PhysicalMonitor[] physicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyPhysicalMonitors(
        uint physicalMonitorArraySize,
        PhysicalMonitor[] physicalMonitorArray);

    /// <summary>
    /// Reads the monitor's brightness range and current value. Failure is the
    /// normal way to discover that a monitor does not really support DDC/CI,
    /// regardless of what it advertises.
    /// </summary>
    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorBrightness(
        nint hMonitor,
        out uint minimumBrightness,
        out uint currentBrightness,
        out uint maximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetMonitorBrightness(nint hMonitor, uint brightness);
}
