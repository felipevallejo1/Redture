using Microsoft.Extensions.Logging;
using Redture.Core.Color;

namespace Redture.Platform.Abstractions.Gamma;

/// <summary>
/// Used where no LUT backend exists yet. Reports that colour temperature is
/// unsupported so the UI can say so plainly rather than offering a slider that
/// silently does nothing.
/// </summary>
public sealed class NullGammaController : IGammaController
{
    private readonly ILogger<NullGammaController> _logger;

    public NullGammaController(ILogger<NullGammaController> logger)
    {
        _logger = logger;
        _logger.LogDebug("No gamma backend on this platform; colour temperature is unavailable.");
    }

    public bool IsSupported => false;

    public bool LastRampRejected => false;

    public void Apply(GammaRamp ramp)
    {
    }

    public void Refresh()
    {
    }

    public void ResetToLinear()
    {
    }

    public void Dispose()
    {
    }
}
