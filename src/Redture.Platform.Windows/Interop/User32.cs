using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>
/// P/Invoke declarations for <c>user32.dll</c>.
/// </summary>
/// <remarks>
/// <c>DllImport</c> rather than the newer <c>LibraryImport</c> source generator:
/// these entry points take structs containing fixed-length string buffers
/// (<c>ByValTStr</c>), which the generator does not marshal. The generator is
/// used for the blittable signatures instead — see <see cref="Shcore"/>.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class User32
{
    /// <summary>The device is part of the desktop (i.e. actually in use).</summary>
    internal const uint DisplayDeviceAttachedToDesktop = 0x00000001;

    /// <summary>The device is the primary display.</summary>
    internal const uint DisplayDevicePrimaryDevice = 0x00000004;

    /// <summary>Ask <see cref="EnumDisplaySettingsW"/> for the mode in use right now.</summary>
    internal const int EnumCurrentSettings = -1;

    /// <summary>Return the nearest monitor when a point falls outside every display.</summary>
    internal const uint MonitorDefaultToNearest = 0x00000002;

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
}
