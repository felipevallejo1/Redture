namespace Redture.Platform.Abstractions.Brightness;

/// <summary>
/// Drives real backlight brightness, where the hardware allows it.
/// </summary>
/// <remarks>
/// <para>
/// Real backlight is strictly better than dimming pixels while it lasts: less
/// light reaching the eye, less power drawn, and no loss of contrast. It simply
/// runs out — which is what the software overlay is for. See
/// <c>docs/adr/0003-unified-brightness-slider.md</c>.
/// </para>
/// <para>
/// <see cref="SetBrightness"/> must be assumed slow. DDC/CI travels over the
/// cable's I²C channel and measures around 60 ms per call on real hardware,
/// against a slider that produces events every 16 ms. Implementations are
/// therefore required to coalesce: apply the newest value, discard everything
/// queued behind it, and never block the caller.
/// </para>
/// </remarks>
public interface IHardwareBrightnessController : IDisposable
{
    /// <summary>True when at least one display accepts backlight control.</summary>
    bool IsAvailable { get; }

    /// <summary>Displays under control, for diagnostics and the UI.</summary>
    IReadOnlyList<HardwareBrightnessTarget> Targets { get; }

    /// <summary>
    /// Backlight level read from the hardware at the last <see cref="Refresh"/>,
    /// 0–100, or null when nothing is controllable. Used on first run to adopt
    /// the level the user already had rather than overwriting it.
    /// </summary>
    double? CurrentPercent { get; }

    /// <summary>
    /// Re-detects controllable displays. Called at startup and after a display
    /// change, since handles do not survive a topology change.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Requests a backlight level, 0–100. Returns immediately; the value is
    /// applied asynchronously and superseded if a newer one arrives first.
    /// </summary>
    void SetBrightness(double percent);

    /// <summary>
    /// Puts every target back to the level it had when Redture took over.
    /// Called when corrections are switched off and on shutdown: quitting must
    /// never leave a monitor dark with no application left to fix it.
    /// </summary>
    void RestoreInitial();
}
