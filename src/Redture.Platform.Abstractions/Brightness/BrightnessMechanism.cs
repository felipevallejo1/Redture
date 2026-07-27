namespace Redture.Platform.Abstractions.Brightness;

/// <summary>
/// How a display's backlight is actually driven.
/// </summary>
public enum BrightnessMechanism
{
    /// <summary>No hardware control; the display can only be dimmed in software.</summary>
    None = 0,

    /// <summary>
    /// DDC/CI: brightness commands sent over the video cable's I²C channel.
    /// The usual path for external monitors. Slow (tens of milliseconds per
    /// call) and not universally implemented, even by monitors that advertise it.
    /// </summary>
    DdcCi = 1,

    /// <summary>
    /// WMI <c>WmiMonitorBrightnessMethods</c>: the built-in panel of a laptop,
    /// driven through the same path as the keyboard's brightness keys.
    /// </summary>
    WmiPanel = 2,
}
