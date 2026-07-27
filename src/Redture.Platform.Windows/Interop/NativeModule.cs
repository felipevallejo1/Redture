using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>
/// The current process's module handle, resolved once and shared by everything
/// that has to register a window class or create a window.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NativeModule
{
    internal static nint Handle { get; } = Kernel32.GetModuleHandleW(null);
}
