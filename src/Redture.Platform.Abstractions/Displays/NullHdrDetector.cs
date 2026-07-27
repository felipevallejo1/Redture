namespace Redture.Platform.Abstractions.Displays;

/// <summary>
/// Reports no HDR displays, on platforms where the question has not been wired
/// up yet.
/// </summary>
public sealed class NullHdrDetector : IHdrDetector
{
    public IReadOnlySet<string> FindHdrDisplays() => new HashSet<string>();
}
