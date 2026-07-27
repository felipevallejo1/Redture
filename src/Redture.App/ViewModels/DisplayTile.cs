using Redture.Platform.Abstractions.Displays;

namespace Redture.App.ViewModels;

/// <summary>
/// One display, ready to be drawn as a rectangle on a small map.
/// </summary>
/// <param name="Name">Monitor name.</param>
/// <param name="Resolution">Human-readable size, e.g. <c>1920 × 1080</c>.</param>
/// <param name="DeviceId">Adapter output path, shown only on hover.</param>
/// <param name="IsPrimary">Whether this is the primary display.</param>
/// <param name="X">Left edge within the map, in pixels.</param>
/// <param name="Y">Top edge within the map, in pixels.</param>
/// <param name="Width">Width within the map, in pixels.</param>
/// <param name="Height">Height within the map, in pixels.</param>
public sealed record DisplayTile(
    string Name,
    string Resolution,
    string DeviceId,
    bool IsPrimary,
    double X,
    double Y,
    double Width,
    double Height)
{
    /// <summary>
    /// Scales a set of displays into a small map that keeps their real relative
    /// positions and sizes.
    /// </summary>
    /// <remarks>
    /// A list of device paths and coordinate pairs is technically complete and
    /// tells you nothing at a glance. Drawn to scale, a second monitor sitting
    /// to the left and slightly lower is obvious without reading a single
    /// number — which is the whole question anyone opens this section to answer.
    /// </remarks>
    public static IReadOnlyList<DisplayTile> Layout(
        IReadOnlyList<DisplayInfo> displays,
        double canvasWidth,
        double canvasHeight)
    {
        ArgumentNullException.ThrowIfNull(displays);

        if (displays.Count == 0)
        {
            return [];
        }

        int left = displays.Min(d => d.Bounds.X);
        int top = displays.Min(d => d.Bounds.Y);
        int right = displays.Max(d => d.Bounds.Right);
        int bottom = displays.Max(d => d.Bounds.Bottom);

        double spanX = Math.Max(right - left, 1);
        double spanY = Math.Max(bottom - top, 1);

        // One scale for both axes, so the shapes stay in proportion. Scaling
        // each axis to fill would turn a 16:9 monitor into whatever the card
        // happens to be, which is worse than leaving space around the edges.
        double scale = Math.Min(canvasWidth / spanX, canvasHeight / spanY);

        double offsetX = (canvasWidth - (spanX * scale)) / 2d;
        double offsetY = (canvasHeight - (spanY * scale)) / 2d;

        List<DisplayTile> tiles = [];

        foreach (DisplayInfo display in displays)
        {
            tiles.Add(new DisplayTile(
                Name: display.Name,
                Resolution: $"{display.Bounds.Width} × {display.Bounds.Height}",
                DeviceId: display.Id,
                IsPrimary: display.IsPrimary,
                X: offsetX + ((display.Bounds.X - left) * scale),
                Y: offsetY + ((display.Bounds.Y - top) * scale),

                // A one-pixel gap so adjacent monitors read as two rectangles
                // rather than one wide one.
                Width: Math.Max((display.Bounds.Width * scale) - 2d, 8d),
                Height: Math.Max((display.Bounds.Height * scale) - 2d, 8d)));
        }

        return tiles;
    }
}
