using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.Overlay;

/// <summary>
/// Registers the window class shared by every overlay window.
/// </summary>
/// <remarks>
/// The class does two things that remove the need for any managed rendering:
/// its background brush is solid black, so the system erases the window black
/// on its own, and its window procedure is <c>DefWindowProcW</c> itself, so no
/// message ever crosses back into managed code. What remains is a black
/// surface whose only variable is its layered alpha.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class OverlayWindowClass
{
    private const string ClassName = "RedtureOverlayWindow";

    /// <summary>
    /// Kept in a static field so the GC never collects the delegate whose
    /// function pointer was handed to the window class. Collecting it would
    /// leave the class pointing at freed memory.
    /// </summary>
    private static readonly WindowProcedure DefaultProcedure = User32.DefWindowProcW;

    private static readonly Lock Gate = new();

    private static bool _registered;

    /// <summary>
    /// Registers the class on first use and returns its name. Safe to call
    /// repeatedly.
    /// </summary>
    internal static string EnsureRegistered()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return ClassName;
            }

            WindowClassEx windowClass = new()
            {
                cbSize = (uint)Marshal.SizeOf<WindowClassEx>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(DefaultProcedure),
                hInstance = NativeModule.Handle,
                hbrBackground = Gdi32.CreateSolidBrush(Gdi32.Black),
                lpszClassName = ClassName,
            };

            if (User32.RegisterClassExW(ref windowClass) == 0)
            {
                int error = Marshal.GetLastWin32Error();

                // Already registered by a previous run within this process is
                // fine; anything else means no overlay is possible at all.
                if (error != User32.ErrorClassAlreadyExists)
                {
                    throw new Win32Exception(error, "Could not register the overlay window class.");
                }
            }

            _registered = true;
            return ClassName;
        }
    }
}
