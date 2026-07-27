using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>
/// P/Invoke declarations for <c>shcore.dll</c> (per-monitor DPI, Windows 8.1+).
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class Shcore
{
    /// <summary><c>MDT_EFFECTIVE_DPI</c>: the DPI actually used to scale the UI.</summary>
    internal const int MonitorDpiTypeEffective = 0;

    /// <summary>DPI value that corresponds to a 1.0 scale factor.</summary>
    internal const uint BaselineDpi = 96;

    /// <summary>
    /// Reads a monitor's DPI. Blittable signature, so the LibraryImport source
    /// generator can emit the marshalling stub at compile time.
    /// </summary>
    [LibraryImport("shcore.dll")]
    internal static partial int GetDpiForMonitor(
        nint hmonitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);
}
