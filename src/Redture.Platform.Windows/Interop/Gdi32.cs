using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>
/// P/Invoke declarations for <c>gdi32.dll</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class Gdi32
{
    /// <summary>Opaque black, as a <c>COLORREF</c> (0x00BBGGRR).</summary>
    internal const uint Black = 0x00000000;

    /// <summary>
    /// Creates a solid brush. Used as the window class background so the
    /// overlay paints black without any managed paint handling at all.
    /// </summary>
    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateSolidBrush(uint color);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint hObject);
}
