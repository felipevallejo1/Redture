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

    /// <summary>
    /// Opens a device context for one display adapter output, e.g.
    /// <c>CreateDCW("DISPLAY", "\\\\.\\DISPLAY1", null, 0)</c>. This is the
    /// handle the gamma ramp APIs operate on.
    /// </summary>
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateDCW(string? driver, string device, string? port, nint deviceMode);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(nint hdc);

    /// <summary>
    /// Loads a colour lookup table into the display controller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The array must hold 3 × 256 <c>WORD</c>s: red, then green, then blue.
    /// </para>
    /// <para>
    /// Two failure modes matter. It returns false when Windows' gamma range
    /// restriction rejects a table that deviates too far from linear, which is
    /// what strongly warm settings run into (risk R1). And it returns
    /// <em>true</em> while doing nothing at all when the display is in HDR mode
    /// (risk R2) — a success that has to be detected some other way.
    /// </para>
    /// </remarks>
    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetDeviceGammaRamp(nint hdc, ushort[] ramp);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetDeviceGammaRamp(nint hdc, ushort[] ramp);
}
