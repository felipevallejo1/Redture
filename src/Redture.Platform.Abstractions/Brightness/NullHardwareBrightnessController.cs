using Microsoft.Extensions.Logging;

namespace Redture.Platform.Abstractions.Brightness;

/// <summary>
/// Used where no backlight backend exists. Reports nothing controllable, which
/// makes the brightness mapper fall back to its software-only path — the same
/// path a monitor that refuses DDC/CI takes on Windows.
/// </summary>
public sealed class NullHardwareBrightnessController : IHardwareBrightnessController
{
    private readonly ILogger<NullHardwareBrightnessController> _logger;

    public NullHardwareBrightnessController(ILogger<NullHardwareBrightnessController> logger) => _logger = logger;

    public bool IsAvailable => false;

    public IReadOnlyList<HardwareBrightnessTarget> Targets => [];

    public double? CurrentPercent => null;

    public void Refresh() =>
        _logger.LogDebug("No backlight backend on this platform; the whole brightness range stays in software.");

    public void SetBrightness(double percent)
    {
    }

    public void RestoreInitial()
    {
    }

    public void Dispose()
    {
    }
}
