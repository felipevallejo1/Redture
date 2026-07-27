using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Brightness;

namespace Redture.Platform.Windows.Brightness;

/// <summary>
/// The built-in panel of a laptop, driven through WMI.
/// </summary>
/// <remarks>
/// <para>
/// This is the same path the keyboard's brightness keys use, so it works on
/// panels that have no DDC/CI channel at all — which is most of them. It exists
/// only on machines with an integrated display: on a desktop the WMI classes
/// are simply not present, and discovery quietly finds nothing.
/// </para>
/// <para>
/// The scale is a plain 0–100 percentage, unlike DDC/CI where the display
/// declares its own range.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class WmiPanelBacklightTarget : BacklightTarget
{
    private const string Scope = @"root\WMI";

    /// <summary>Seconds the panel is given to complete the change.</summary>
    private const uint WriteTimeoutSeconds = 1;

    private readonly ManagementObject _methods;

    private WmiPanelBacklightTarget(string name, ManagementObject methods, double initialPercent)
        : base(displayId: string.Empty, name, BrightnessMechanism.WmiPanel, initialPercent)
    {
        // The internal panel has no adapter output id we can match against the
        // display enumerator, hence the empty DisplayId: there is exactly one
        // of these, and it is always the built-in screen.
        _methods = methods;
    }

    /// <summary>
    /// Finds the built-in panel, or returns null on any machine that does not
    /// have one.
    /// </summary>
    public static WmiPanelBacklightTarget? TryDiscover(ILogger logger)
    {
        try
        {
            double? current = ReadCurrentPercent();
            if (current is null)
            {
                logger.LogDebug("No WMI backlight instance; this machine has no controllable built-in panel.");
                return null;
            }

            using ManagementClass methodClass = new(
                new ManagementScope(Scope),
                new ManagementPath("WmiMonitorBrightnessMethods"),
                options: null);

            foreach (ManagementBaseObject instance in methodClass.GetInstances())
            {
                if (instance is ManagementObject methods)
                {
                    logger.LogInformation(
                        "Backlight control available on the built-in panel via WMI, currently at {Percent:0}%.",
                        current.Value);

                    return new WmiPanelBacklightTarget("Built-in display", methods, current.Value);
                }

                instance.Dispose();
            }

            return null;
        }
        catch (ManagementException ex)
        {
            // Desktops answer "Incompatible" here because they have no built-in
            // panel. That is the expected outcome on most machines, not a
            // failure, so it gets one clean line and no stack trace — a log
            // full of expected exceptions is a log nobody reads.
            logger.LogDebug(
                "No WMI backlight control on this machine ({Reason}); only external monitors can be driven.",
                ex.Message.Trim());
            return null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or COMException)
        {
            logger.LogWarning(ex, "WMI backlight control could not be queried.");
            return null;
        }
    }

    private static double? ReadCurrentPercent()
    {
        using ManagementObjectSearcher searcher = new(Scope, "SELECT CurrentBrightness FROM WmiMonitorBrightness");

        foreach (ManagementBaseObject instance in searcher.Get())
        {
            using (instance)
            {
                object? value = instance["CurrentBrightness"];
                if (value is not null)
                {
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
                }
            }
        }

        return null;
    }

    protected override int ToDeviceLevel(double percent) => (int)Math.Round(percent);

    protected override bool TryWriteLevel(int level)
    {
        _methods.InvokeMethod(
            "WmiSetBrightness",
            [WriteTimeoutSeconds, (byte)Math.Clamp(level, 0, 100)]);

        return true;
    }

    public override void Dispose()
    {
        _methods.Dispose();
        base.Dispose();
    }
}
