namespace Redture.Platform.Abstractions.Displays;

/// <summary>
/// Lists the displays currently attached to the desktop.
/// </summary>
/// <remarks>
/// Results are read live from the OS on every call rather than cached: display
/// topology changes constantly (hot-plug, docking, resolution changes, RDP) and
/// a stale list means overlays covering the wrong area. Notification that it
/// changed comes from <c>ISystemEvents.DisplaysChanged</c> rather than from
/// here, so that this stays a plain question with a plain answer.
/// </remarks>
public interface IDisplayEnumerator
{
    IReadOnlyList<DisplayInfo> GetDisplays();
}
