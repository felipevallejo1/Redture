using Microsoft.Extensions.Logging;

namespace Redture.Platform.Abstractions.Displays;

/// <summary>
/// Fallback for any situation with no usable display backend — an unsupported
/// operating system, or a Linux session with no X server. It reports no
/// displays instead of throwing, so the app still starts, the tray icon still
/// works, and the UI can say plainly what is unavailable.
/// </summary>
public sealed class UnsupportedDisplayEnumerator : IDisplayEnumerator
{
    private readonly ILogger<UnsupportedDisplayEnumerator> _logger;

    public UnsupportedDisplayEnumerator(ILogger<UnsupportedDisplayEnumerator> logger)
    {
        _logger = logger;
        _logger.LogWarning(
            "No display backend for this platform ({Platform}); display-dependent features are disabled.",
            Environment.OSVersion.Platform);
    }

    public IReadOnlyList<DisplayInfo> GetDisplays() => [];
}
