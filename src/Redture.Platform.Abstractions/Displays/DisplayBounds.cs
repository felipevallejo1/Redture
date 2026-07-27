namespace Redture.Platform.Abstractions.Displays;

/// <summary>
/// A rectangle in virtual-screen coordinates, in physical pixels.
/// </summary>
/// <remarks>
/// Deliberately not <c>System.Drawing.Rectangle</c> nor an Avalonia type: the
/// abstraction layer must not drag a UI framework or a Windows-only assembly
/// into the platform backends.
/// <para>
/// Coordinates can be negative — a monitor placed to the left of the primary
/// one starts at a negative X. The overlay windows rely on that, so any code
/// touching these values must never assume a zero-based origin.
/// </para>
/// </remarks>
public readonly record struct DisplayBounds(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    /// <summary>A point guaranteed to fall inside this display, used to resolve
    /// the monitor handle it belongs to.</summary>
    public (int X, int Y) Center => (X + (Width / 2), Y + (Height / 2));

    public override string ToString() => $"{Width}x{Height} @ ({X},{Y})";
}
