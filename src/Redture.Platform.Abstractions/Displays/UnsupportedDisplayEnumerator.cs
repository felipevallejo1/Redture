using Microsoft.Extensions.Logging;

namespace Redture.Platform.Abstractions.Displays;

/// <summary>
/// Fallback used on operating systems that have no backend yet (Linux and
/// macOS land in stages 5 and 6). It reports no displays instead of throwing,
/// so the app still starts, the tray icon still works, and the UI can show an
/// honest "not supported on this platform" message.
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
