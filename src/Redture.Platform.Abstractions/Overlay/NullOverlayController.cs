using Microsoft.Extensions.Logging;

namespace Redture.Platform.Abstractions.Overlay;

/// <summary>
/// No-op overlay for platforms without a backend yet. Accepts every call and
/// reports the opacity it was asked for, so the UI and the coordinator behave
/// identically — only the screen stays untouched.
/// </summary>
public sealed class NullOverlayController : IOverlayController
{
    private readonly ILogger<NullOverlayController> _logger;

    public NullOverlayController(ILogger<NullOverlayController> logger) => _logger = logger;

    public double Opacity { get; private set; }

    public void SetOpacity(double opacity)
    {
        Opacity = Math.Clamp(opacity, 0d, 1d);
        _logger.LogDebug("Overlay opacity {Opacity:0.###} requested, but no backend exists on this platform.", Opacity);
    }

    public void Refresh()
    {
    }

    public void Dispose()
    {
    }
}
