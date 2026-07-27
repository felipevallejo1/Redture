namespace Redture.Platform.Abstractions.Displays;

/// <summary>
/// Lists the displays currently attached to the desktop.
/// </summary>
/// <remarks>
/// Results are read live from the OS on every call rather than cached: display
/// topology changes constantly (hot-plug, docking, resolution changes, RDP) and
/// a stale list means overlays covering the wrong area. Change notifications
/// are added in stage 1, alongside the overlay that needs to react to them.
/// </remarks>
public interface IDisplayEnumerator
{
    IReadOnlyList<DisplayInfo> GetDisplays();
}
