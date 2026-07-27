namespace Redture.Platform.Abstractions.Startup;

/// <summary>Auto-start is not wired up on this platform yet.</summary>
public sealed class NullAutoStartService : IAutoStartService
{
    public bool IsSupported => false;

    public bool IsEnabled => false;

    public bool SetEnabled(bool enabled) => false;
}
