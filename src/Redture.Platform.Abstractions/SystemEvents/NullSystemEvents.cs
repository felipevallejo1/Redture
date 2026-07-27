using Microsoft.Extensions.Logging;

namespace Redture.Platform.Abstractions.SystemEvents;

/// <summary>
/// No-op notification source for platforms without a backend yet. The events
/// exist but never fire.
/// </summary>
public sealed class NullSystemEvents : ISystemEvents
{
    private readonly ILogger<NullSystemEvents> _logger;

    public NullSystemEvents(ILogger<NullSystemEvents> logger) => _logger = logger;

#pragma warning disable CS0067 // Never raised on this platform, by design.
    public event EventHandler? DisplaysChanged;

    public event EventHandler? SessionResumed;

    public event EventHandler? PanicRequested;
#pragma warning restore CS0067

    public string? PanicHotkeyDescription => null;

    public void Start() =>
        _logger.LogDebug("No system-event backend on this platform; display changes and the panic hotkey are unavailable.");

    public void Dispose()
    {
    }
}
