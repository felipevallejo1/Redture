namespace Redture.Platform.Abstractions.Displays;

/// <summary>
/// Reports which displays are currently running in HDR mode.
/// </summary>
/// <remarks>
/// This exists for one reason: on a display in HDR mode, loading a gamma ramp
/// <em>succeeds and does nothing</em>. The call returns true, the driver stores
/// the table, and the picture never changes. Without an independent check there
/// is no way to tell that apart from working correctly, and the user is left
/// with a colour temperature slider that silently does nothing.
/// </remarks>
public interface IHdrDetector
{
    /// <summary>
    /// Ids of displays where HDR is on, matching <see cref="DisplayInfo.Id"/>.
    /// An empty set means either no HDR anywhere or that the state could not be
    /// determined — the caller treats both the same way, by proceeding.
    /// </summary>
    IReadOnlySet<string> FindHdrDisplays();
}
