using Redture.Core.Settings;

namespace Redture.Core.Brightness;

/// <summary>
/// Maps the single user-facing brightness value onto the two mechanisms that
/// implement it.
/// </summary>
/// <remarks>
/// <para>
/// See <c>docs/adr/0003-unified-brightness-slider.md</c>. The user gets one
/// slider; whether a given position is delivered by the panel's backlight or by
/// a black overlay is an implementation detail they should never have to think
/// about.
/// </para>
/// <para>
/// With hardware control available the range is split in two:
/// </para>
/// <code>
/// 100% ─────────────  backlight 100%,  overlay 0
///  30% ─────────────  backlight   0%,  overlay 0        ← split point
///   0% ─────────────  backlight   0%,  overlay maxOpacity
/// </code>
/// <para>
/// Without it — no DDC/CI, an unsupported panel, a monitor that simply refuses
/// (risk R12) — the split collapses and the entire range is driven by the
/// overlay. That is the documented degradation path, not a special case.
/// </para>
/// <para>
/// The interpolation is linear in opacity rather than in luminance. The
/// compositor blends gamma-encoded values, so a linear change in opacity is
/// already close to a linear change in *perceived* brightness; converting to
/// linear light first would make the slider feel dead across its upper half.
/// </para>
/// </remarks>
public static class BrightnessMapper
{
    /// <summary>
    /// Brightness value below which the backlight is exhausted and the overlay
    /// takes over. Chosen low enough that most of the slider drives real
    /// backlight — which is easier on the eyes and on the battery than dimming
    /// pixels — while leaving a usable range below the hardware floor.
    /// </summary>
    public const double DefaultHardwareSplitPoint = 30d;

    /// <summary>
    /// Splits <paramref name="brightness"/> into a backlight level and an
    /// overlay opacity.
    /// </summary>
    /// <param name="brightness">Perceived brightness, 0–100.</param>
    /// <param name="maxOverlayOpacity">
    /// Opacity applied at brightness 0. Capped by
    /// <see cref="AppSettings.AbsoluteMaxOverlayOpacity"/> so the screen can
    /// never go fully black.
    /// </param>
    /// <param name="hardwareAvailable">
    /// Whether the panel's backlight can actually be driven.
    /// </param>
    /// <param name="splitPoint">Handover point; ignored when there is no hardware control.</param>
    public static BrightnessPlan Map(
        double brightness,
        double maxOverlayOpacity,
        bool hardwareAvailable,
        double splitPoint = DefaultHardwareSplitPoint)
    {
        brightness = Math.Clamp(brightness, AppSettings.MinBrightness, AppSettings.MaxBrightness);
        maxOverlayOpacity = Math.Clamp(maxOverlayOpacity, 0d, AppSettings.AbsoluteMaxOverlayOpacity);
        splitPoint = Math.Clamp(splitPoint, 1d, AppSettings.MaxBrightness - 1d);

        if (!hardwareAvailable)
        {
            // Whole range in software: full brightness means no overlay, zero
            // means the configured maximum opacity.
            double fraction = brightness / AppSettings.MaxBrightness;
            return new BrightnessPlan(null, maxOverlayOpacity * (1d - fraction));
        }

        if (brightness >= splitPoint)
        {
            // Upper segment: rescale [splitPoint, 100] onto a [0, 100] backlight
            // request. No overlay at all up here.
            double hardware = (brightness - splitPoint) / (AppSettings.MaxBrightness - splitPoint) * AppSettings.MaxBrightness;
            return new BrightnessPlan(hardware, 0d);
        }

        // Lower segment: backlight is already exhausted, the overlay ramps from
        // nothing at the split point to maxOverlayOpacity at zero. The two
        // segments meet at exactly zero opacity, so the handover is invisible.
        double overlayFraction = 1d - (brightness / splitPoint);
        return new BrightnessPlan(0d, maxOverlayOpacity * overlayFraction);
    }

    /// <summary>
    /// The inverse of <see cref="Map"/>: given what the display is actually
    /// doing, works out the slider position that would produce it.
    /// </summary>
    /// <remarks>
    /// Used on first run to adopt the backlight level the user already had.
    /// Without it, a fresh install would blast a monitor deliberately set to
    /// 20% up to full brightness simply because 100 is the default value of a
    /// setting nobody has touched yet.
    /// </remarks>
    public static double Unmap(
        double hardwarePercent,
        double overlayOpacity,
        double maxOverlayOpacity,
        bool hardwareAvailable,
        double splitPoint = DefaultHardwareSplitPoint)
    {
        hardwarePercent = Math.Clamp(hardwarePercent, AppSettings.MinBrightness, AppSettings.MaxBrightness);
        maxOverlayOpacity = Math.Clamp(maxOverlayOpacity, 0d, AppSettings.AbsoluteMaxOverlayOpacity);
        splitPoint = Math.Clamp(splitPoint, 1d, AppSettings.MaxBrightness - 1d);

        // With no overlay budget there is nothing to invert on that side.
        double overlayFraction = maxOverlayOpacity <= 0d
            ? 0d
            : Math.Clamp(overlayOpacity / maxOverlayOpacity, 0d, 1d);

        if (!hardwareAvailable)
        {
            return AppSettings.MaxBrightness * (1d - overlayFraction);
        }

        return overlayFraction > 0d
            ? splitPoint * (1d - overlayFraction)
            : splitPoint + (hardwarePercent / AppSettings.MaxBrightness * (AppSettings.MaxBrightness - splitPoint));
    }
}
