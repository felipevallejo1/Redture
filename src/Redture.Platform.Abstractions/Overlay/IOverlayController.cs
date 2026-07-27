namespace Redture.Platform.Abstractions.Overlay;

/// <summary>
/// Drives the black dimming overlay that lets Redture keep reducing perceived
/// brightness after the monitor's own control has bottomed out.
/// </summary>
/// <remarks>
/// <para>
/// The overlay is black, never tinted. Blending black at opacity <c>a</c> is
/// arithmetically <c>(1 - a) × content</c> — a pure multiply that leaves black
/// pixels black and the contrast ratio intact. A tinted overlay would raise the
/// black floor instead; see
/// <c>docs/adr/0002-gamma-for-colour-overlay-for-brightness.md</c>.
/// </para>
/// <para>
/// Implementations own one window per display and must be used from a single
/// thread that owns a message loop — on Windows, window handles belong to the
/// thread that created them.
/// </para>
/// </remarks>
public interface IOverlayController : IDisposable
{
    /// <summary>Opacity currently applied, 0–1.</summary>
    double Opacity { get; }

    /// <summary>
    /// Sets the overlay opacity. <c>0</c> hides it entirely rather than leaving
    /// a fully transparent window composited for nothing.
    /// </summary>
    void SetOpacity(double opacity);

    /// <summary>
    /// Re-reads the display topology and adds, moves or removes overlay windows
    /// to match. Called after a display change, debounced by the caller: these
    /// events arrive in bursts and rebuilding on each one is itself a source of
    /// flicker.
    /// </summary>
    void Refresh();
}
