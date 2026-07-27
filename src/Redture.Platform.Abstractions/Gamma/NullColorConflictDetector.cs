namespace Redture.Platform.Abstractions.Gamma;

/// <summary>
/// Names nothing, on platforms with no process list worth consulting yet. A
/// conflict is still detected empirically; it just gets a generic description.
/// </summary>
public sealed class NullColorConflictDetector : IColorConflictDetector
{
    public IReadOnlyList<string> FindRunningColorApplications() => [];
}
