using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>
/// P/Invoke declarations for <c>kernel32.dll</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class Kernel32
{
    /// <summary>
    /// Returns the module handle for the current process when
    /// <paramref name="lpModuleName"/> is null.
    /// </summary>
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial nint GetModuleHandleW(string? lpModuleName);
}
