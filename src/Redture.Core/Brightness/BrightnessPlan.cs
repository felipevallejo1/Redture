namespace Redture.Core.Brightness;

/// <summary>
/// How a single brightness value is split across the two mechanisms that can
/// deliver it.
/// </summary>
/// <param name="HardwareBrightness">
/// Backlight level to request from the panel, 0–100, or <see langword="null"/>
/// when no hardware control is available and the whole range is handled in
/// software.
/// </param>
/// <param name="OverlayOpacity">
/// Opacity of the black dimming overlay, 0–1. Zero means no overlay at all.
/// </param>
public readonly record struct BrightnessPlan(double? HardwareBrightness, double OverlayOpacity)
{
    /// <summary>True when the overlay is doing nothing and can be hidden.</summary>
    public bool IsOverlayIdle => OverlayOpacity <= 0d;
}
