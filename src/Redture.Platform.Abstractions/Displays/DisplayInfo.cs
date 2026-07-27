namespace Redture.Platform.Abstractions.Displays;

/// <summary>
/// One attached display, as seen by the OS.
/// </summary>
/// <param name="Id">
/// Adapter output identifier, stable while the display stays connected
/// (<c>\\.\DISPLAY1</c> on Windows). Used to open a device context for the
/// gamma ramp and to key the overlay window that covers this display.
/// </param>
/// <param name="Name">Human-readable name shown in the UI.</param>
/// <param name="Bounds">Position and size in virtual-screen pixels.</param>
/// <param name="IsPrimary">Whether this is the primary display.</param>
/// <param name="ScaleFactor">
/// DPI scaling, where 1.0 means 96 DPI. Reported per display: a mixed-DPI setup
/// is the usual reason overlay windows end up the wrong size.
/// </param>
public sealed record DisplayInfo(
    string Id,
    string Name,
    DisplayBounds Bounds,
    bool IsPrimary,
    double ScaleFactor)
{
    public override string ToString() =>
        $"{Name} [{Id}] {Bounds} scale {ScaleFactor:0.##}{(IsPrimary ? " (primary)" : string.Empty)}";
}
