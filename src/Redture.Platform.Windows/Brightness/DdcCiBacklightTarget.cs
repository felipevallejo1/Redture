using System.Runtime.Versioning;
using Redture.Platform.Abstractions.Brightness;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.Brightness;

/// <summary>
/// An external monitor driven over DDC/CI.
/// </summary>
/// <remarks>
/// The monitor reports its own brightness range, which is <em>not</em>
/// guaranteed to be 0–100 — some report 0–255, some a narrow band well away
/// from zero. Percentages are therefore mapped into whatever range the display
/// declares rather than being sent as-is.
/// <para>
/// The handle belongs to an array owned by <see cref="DdcCiSession"/> and must
/// not be released here.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class DdcCiBacklightTarget : BacklightTarget
{
    private readonly nint _handle;
    private readonly uint _minimum;
    private readonly uint _maximum;

    private DdcCiBacklightTarget(
        string displayId,
        string name,
        nint handle,
        uint minimum,
        uint maximum,
        double initialPercent)
        : base(displayId, name, BrightnessMechanism.DdcCi, initialPercent)
    {
        _handle = handle;
        _minimum = minimum;
        _maximum = maximum;
    }

    /// <summary>
    /// Probes a physical monitor and returns a target when it genuinely
    /// supports brightness control.
    /// </summary>
    /// <remarks>
    /// The probe is the only reliable test. Plenty of monitors expose a DDC/CI
    /// channel and then fail the brightness capability, and plenty of docks and
    /// adapters break it even when the monitor supports it (risk R12).
    /// </remarks>
    public static DdcCiBacklightTarget? TryProbe(string displayId, string displayName, PhysicalMonitor monitor)
    {
        if (!Dxva2.GetMonitorBrightness(monitor.hPhysicalMonitor, out uint minimum, out uint current, out uint maximum))
        {
            return null;
        }

        if (maximum <= minimum)
        {
            return null; // Degenerate range; nothing meaningful to drive.
        }

        double initialPercent = (current - minimum) / (double)(maximum - minimum) * 100d;

        string name = string.IsNullOrWhiteSpace(monitor.szPhysicalMonitorDescription)
            ? displayName
            : monitor.szPhysicalMonitorDescription;

        return new DdcCiBacklightTarget(displayId, name, monitor.hPhysicalMonitor, minimum, maximum, initialPercent);
    }

    protected override int ToDeviceLevel(double percent) =>
        (int)Math.Round(_minimum + (percent / 100d * (_maximum - _minimum)));

    protected override bool TryWriteLevel(int level) =>
        Dxva2.SetMonitorBrightness(_handle, (uint)Math.Clamp(level, _minimum, _maximum));
}
