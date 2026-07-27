using System.ComponentModel;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Overlay;

namespace Redture.Platform.Windows.Overlay;

/// <inheritdoc cref="IOverlayController" />
/// <remarks>
/// One window per display rather than a single window spanning the virtual
/// desktop. A spanning window would be simpler, but it breaks the moment the
/// displays are not a perfect rectangle — an L-shaped arrangement leaves gaps
/// the overlay would cover with nothing behind them — and it makes per-monitor
/// DPI impossible to handle correctly.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsOverlayController : IOverlayController
{
    private readonly IDisplayEnumerator _displayEnumerator;
    private readonly ILogger<WindowsOverlayController> _logger;

    /// <summary>Live overlay windows, keyed by display id.</summary>
    private readonly Dictionary<string, OverlayWindow> _windows = new(StringComparer.OrdinalIgnoreCase);

    private double _opacity;
    private bool _disposed;

    public WindowsOverlayController(
        IDisplayEnumerator displayEnumerator,
        ILogger<WindowsOverlayController> logger)
    {
        _displayEnumerator = displayEnumerator;
        _logger = logger;
    }

    public double Opacity => _opacity;

    public void SetOpacity(double opacity)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _opacity = Math.Clamp(opacity, 0d, 1d);
        byte alpha = ToAlpha(_opacity);

        // Stay lazy: with no dimming requested and nothing created yet, Redture
        // owns no windows at all and costs the compositor nothing.
        if (alpha == 0 && _windows.Count == 0)
        {
            return;
        }

        if (_windows.Count == 0)
        {
            SyncWindows();
        }

        ApplyAlpha(alpha);
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_windows.Count == 0)
        {
            return; // Nothing materialised yet; the next SetOpacity will build it.
        }

        SyncWindows();
        ApplyAlpha(ToAlpha(_opacity));
    }

    private void ApplyAlpha(byte alpha)
    {
        foreach (OverlayWindow window in _windows.Values)
        {
            window.SetAlpha(alpha);
        }
    }

    /// <summary>
    /// Reconciles the set of overlay windows with the displays currently
    /// attached: repositions the ones that moved, creates windows for new
    /// displays and destroys those whose display is gone.
    /// </summary>
    private void SyncWindows()
    {
        IReadOnlyList<DisplayInfo> displays = _displayEnumerator.GetDisplays();
        HashSet<string> present = new(StringComparer.OrdinalIgnoreCase);

        foreach (DisplayInfo display in displays)
        {
            present.Add(display.Id);

            if (_windows.TryGetValue(display.Id, out OverlayWindow? existing))
            {
                existing.SetBounds(display.Bounds);
                continue;
            }

            try
            {
                _windows[display.Id] = OverlayWindow.Create(display, _logger);
            }
            catch (Win32Exception ex)
            {
                // One display failing must not cost the user dimming on the
                // others; the next refresh will retry.
                _logger.LogError(ex, "Could not create an overlay for {DisplayId}.", display.Id);
            }
        }

        foreach (string staleId in _windows.Keys.Where(id => !present.Contains(id)).ToList())
        {
            _windows[staleId].Dispose();
            _windows.Remove(staleId);
            _logger.LogDebug("Overlay for {DisplayId} removed: the display is gone.", staleId);
        }
    }

    /// <summary>
    /// Converts opacity to the 0–255 alpha the layered-window API takes.
    /// Rounding here is also what makes writes idempotent: two slider positions
    /// that map to the same byte produce no OS call at all.
    /// </summary>
    private static byte ToAlpha(double opacity) => (byte)Math.Round(opacity * 255d);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (OverlayWindow window in _windows.Values)
        {
            window.Dispose();
        }

        _windows.Clear();
        _logger.LogDebug("Overlay controller disposed; all overlay windows destroyed.");
    }
}
