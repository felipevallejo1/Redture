using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.Brightness;

/// <summary>
/// Owns the physical-monitor handles obtained for one pass of discovery.
/// </summary>
/// <remarks>
/// <c>DestroyPhysicalMonitors</c> takes the whole array it was given, so handle
/// ownership is per-<c>HMONITOR</c> group, not per-handle. Keeping the arrays
/// here — rather than inside the targets that use them — is what makes that
/// ownership correct, and what lets a topology change release everything in one
/// step before re-discovering.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class DdcCiSession : IDisposable
{
    private readonly List<PhysicalMonitor[]> _handleGroups = [];
    private readonly ILogger _logger;

    private DdcCiSession(ILogger logger) => _logger = logger;

    /// <summary>Targets found in this pass. Valid until the session is disposed.</summary>
    public List<BacklightTarget> Targets { get; } = [];

    /// <summary>
    /// Walks the attached displays and probes each one for DDC/CI brightness
    /// support.
    /// </summary>
    public static DdcCiSession Discover(IReadOnlyList<DisplayInfo> displays, ILogger logger)
    {
        DdcCiSession session = new(logger);

        foreach (DisplayInfo display in displays)
        {
            try
            {
                session.DiscoverDisplay(display);
            }
            catch (Exception ex)
            {
                // One uncooperative monitor must not cost the others their
                // backlight control.
                logger.LogError(ex, "DDC/CI discovery failed for {DisplayId}.", display.Id);
            }
        }

        return session;
    }

    private void DiscoverDisplay(DisplayInfo display)
    {
        (int centreX, int centreY) = display.Bounds.Center;
        PointL centre = new() { X = centreX, Y = centreY };

        nint monitorHandle = User32.MonitorFromPoint(centre, User32.MonitorDefaultToNearest);
        if (monitorHandle == 0)
        {
            return;
        }

        if (!Dxva2.GetNumberOfPhysicalMonitorsFromHMONITOR(monitorHandle, out uint count) || count == 0)
        {
            _logger.LogDebug(
                "No physical monitors behind {DisplayId} (error {Error}); no backlight control there.",
                display.Id,
                Marshal.GetLastWin32Error());
            return;
        }

        PhysicalMonitor[] monitors = new PhysicalMonitor[count];
        if (!Dxva2.GetPhysicalMonitorsFromHMONITOR(monitorHandle, count, monitors))
        {
            _logger.LogDebug(
                "Could not open the DDC/CI channel for {DisplayId} (error {Error}).",
                display.Id,
                Marshal.GetLastWin32Error());
            return;
        }

        // From here on the handles are ours to release, whether or not any of
        // them turns out to be usable.
        _handleGroups.Add(monitors);

        foreach (PhysicalMonitor monitor in monitors)
        {
            DdcCiBacklightTarget? target = DdcCiBacklightTarget.TryProbe(display.Id, display.Name, monitor);

            if (target is null)
            {
                _logger.LogInformation(
                    "{DisplayId} ({DisplayName}) exposes DDC/CI but not brightness control; it will be dimmed in software only.",
                    display.Id,
                    display.Name);
                continue;
            }

            _logger.LogInformation(
                "Backlight control available on {DisplayName} via DDC/CI, currently at {Percent:0}%.",
                target.Name,
                target.InitialPercent);

            Targets.Add(target);
        }
    }

    public void Dispose()
    {
        foreach (BacklightTarget target in Targets)
        {
            target.Dispose();
        }

        Targets.Clear();

        foreach (PhysicalMonitor[] group in _handleGroups)
        {
            Dxva2.DestroyPhysicalMonitors((uint)group.Length, group);
        }

        _handleGroups.Clear();
    }
}
