using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.Displays;

/// <summary>
/// Enumerates attached displays through the GDI display-device APIs.
/// </summary>
/// <remarks>
/// <para>
/// The obvious API for this is <c>EnumDisplayMonitors</c>, but it requires an
/// unmanaged callback. <c>EnumDisplayDevices</c> + <c>EnumDisplaySettings</c>
/// gives the same information with a plain loop and, more importantly, yields
/// the adapter output name (<c>\\.\DISPLAY1</c>) directly — which is exactly
/// the handle the gamma stage needs to open a device context per display.
/// </para>
/// <para>
/// Sizes and positions are physical pixels, not DIPs. The overlay windows added
/// in stage 1 must convert using <see cref="DisplayInfo.ScaleFactor"/>, since
/// Avalonia positions windows in device-independent units.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsDisplayEnumerator : IDisplayEnumerator
{
    /// <summary>
    /// <c>DISPLAY_DEVICE_MIRRORING_DRIVER</c>: pseudo-devices installed by
    /// screen-capture and remote-desktop software. They are not real outputs
    /// and must be skipped, or we would create overlays for phantom displays.
    /// </summary>
    private const uint DisplayDeviceMirroringDriver = 0x00000008;

    private readonly ILogger<WindowsDisplayEnumerator> _logger;

    /// <summary>
    /// Description of the last result, used to log only when the answer changes.
    /// </summary>
    /// <remarks>
    /// Enumeration is called from the gamma write path, which can run twenty
    /// times a second while a transition catches up. Logging every call at
    /// information level buried everything else in the file — a log nobody can
    /// read is worse than no log. What is worth recording is the topology
    /// changing, not the fact that something asked.
    /// </remarks>
    private string _lastLoggedSignature = string.Empty;

    public WindowsDisplayEnumerator(ILogger<WindowsDisplayEnumerator> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        List<DisplayInfo> displays = [];
        int structSize = Marshal.SizeOf<DisplayDevice>();

        for (uint index = 0; ; index++)
        {
            // cb must be re-initialised on every call: the struct is overwritten
            // by the previous iteration.
            DisplayDevice adapter = new() { cb = (uint)structSize };
            if (!User32.EnumDisplayDevicesW(null, index, ref adapter, 0))
            {
                break; // No more adapters.
            }

            if ((adapter.StateFlags & User32.DisplayDeviceAttachedToDesktop) == 0)
            {
                continue; // Present but not part of the desktop.
            }

            if ((adapter.StateFlags & DisplayDeviceMirroringDriver) != 0)
            {
                _logger.LogDebug("Skipping mirroring driver {DeviceName}.", adapter.DeviceName);
                continue;
            }

            DisplayInfo? display = DescribeAdapter(adapter);
            if (display is not null)
            {
                displays.Add(display);
            }
        }

        LogIfChanged(displays);
        return displays;
    }

    /// <summary>
    /// Records the topology the first time it is seen and whenever it changes,
    /// and says nothing on the many calls in between.
    /// </summary>
    private void LogIfChanged(List<DisplayInfo> displays)
    {
        string signature = string.Join(" | ", displays);
        if (signature == _lastLoggedSignature)
        {
            return;
        }

        _lastLoggedSignature = signature;
        _logger.LogInformation("Detected {Count} display(s): {Displays}", displays.Count, signature);
    }

    /// <summary>Turns one attached adapter output into a <see cref="DisplayInfo"/>.</summary>
    private DisplayInfo? DescribeAdapter(DisplayDevice adapter)
    {
        DevMode mode = new() { dmSize = (ushort)Marshal.SizeOf<DevMode>() };
        if (!User32.EnumDisplaySettingsW(adapter.DeviceName, User32.EnumCurrentSettings, ref mode))
        {
            // Happens transiently while a display is being reconfigured; the
            // next enumeration will pick it up.
            _logger.LogWarning(
                "EnumDisplaySettings failed for {DeviceName} (error {Error}); skipping it.",
                adapter.DeviceName,
                Marshal.GetLastWin32Error());
            return null;
        }

        DisplayBounds bounds = new(
            mode.dmPosition.X,
            mode.dmPosition.Y,
            (int)mode.dmPelsWidth,
            (int)mode.dmPelsHeight);

        return new DisplayInfo(
            Id: adapter.DeviceName,
            Name: ResolveFriendlyName(adapter),
            Bounds: bounds,
            IsPrimary: (adapter.StateFlags & User32.DisplayDevicePrimaryDevice) != 0,
            ScaleFactor: ResolveScaleFactor(bounds));
    }

    /// <summary>
    /// Asks the adapter for the monitor attached to it, which carries the model
    /// name shown to the user. Falls back to the adapter description and finally
    /// to the raw device path, so the UI always has something to display.
    /// </summary>
    private static string ResolveFriendlyName(DisplayDevice adapter)
    {
        DisplayDevice monitor = new() { cb = (uint)Marshal.SizeOf<DisplayDevice>() };
        if (User32.EnumDisplayDevicesW(adapter.DeviceName, 0, ref monitor, 0)
            && !string.IsNullOrWhiteSpace(monitor.DeviceString))
        {
            return monitor.DeviceString;
        }

        return string.IsNullOrWhiteSpace(adapter.DeviceString)
            ? adapter.DeviceName
            : adapter.DeviceString;
    }

    /// <summary>
    /// Reads the per-monitor DPI scaling. Any failure degrades to 1.0 rather
    /// than propagating: a wrong scale factor is a cosmetic problem, an
    /// exception here would take down display enumeration entirely.
    /// </summary>
    private double ResolveScaleFactor(DisplayBounds bounds)
    {
        (int centerX, int centerY) = bounds.Center;
        PointL center = new() { X = centerX, Y = centerY };

        nint monitorHandle = User32.MonitorFromPoint(center, User32.MonitorDefaultToNearest);
        if (monitorHandle == 0)
        {
            return 1d;
        }

        try
        {
            const int SOk = 0;
            if (Shcore.GetDpiForMonitor(monitorHandle, Shcore.MonitorDpiTypeEffective, out uint dpiX, out _) == SOk
                && dpiX > 0)
            {
                return dpiX / (double)Shcore.BaselineDpi;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // shcore.dll is Windows 8.1+. Older systems are single-DPI anyway.
            _logger.LogDebug(ex, "Per-monitor DPI is unavailable on this system; assuming 100%.");
        }

        return 1d;
    }
}
