namespace Redture.Platform.Abstractions.SystemEvents;

/// <summary>
/// Always reports a normal desktop, on platforms where this is not wired up.
/// </summary>
public sealed class NullFullscreenDetector : IFullscreenDetector
{
    public bool IsFullscreenActive => false;

#pragma warning disable CS0067 // Never raised on this platform, by design.
    public event EventHandler<bool>? FullscreenStateChanged;
#pragma warning restore CS0067

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
