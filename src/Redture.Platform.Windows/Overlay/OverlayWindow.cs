using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.Overlay;

/// <summary>
/// A single full-screen dimming window covering one display.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a raw <c>HWND</c> rather than an Avalonia window. It needs a
/// combination of extended styles Avalonia does not expose, it must never
/// activate or appear in Alt-Tab, and it should cost as close to nothing as
/// possible: there is no content, no layout pass and no paint loop. The window
/// class carries a black background brush, so the system erases it black and
/// managed code never draws a pixel.
/// </para>
/// <para>
/// Dimming is then just the layered-window alpha. Changing it is a compositor
/// operation — no repaint, no invalidation, no flicker, and effectively no CPU.
/// </para>
/// <para>
/// Window handles belong to the thread that created them: every method here
/// must be called from the same thread, which is the UI thread.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class OverlayWindow : IDisposable
{
    /// <summary>
    /// No border, no title bar, no taskbar presence, no focus stealing, alpha
    /// blending on, and clicks passing straight through to whatever is below.
    /// </summary>
    private const uint ExtendedStyle =
        User32.WsExLayered |
        User32.WsExTransparent |
        User32.WsExTopmost |
        User32.WsExToolWindow |
        User32.WsExNoActivate;

    private readonly ILogger _logger;

    private nint _handle;
    private byte _alpha;
    private bool _visible;

    private OverlayWindow(nint handle, string displayId, DisplayBounds bounds, ILogger logger)
    {
        _handle = handle;
        _logger = logger;
        DisplayId = displayId;
        Bounds = bounds;
    }

    /// <summary>Identifier of the display this window covers.</summary>
    public string DisplayId { get; }

    /// <summary>Area covered, in physical pixels of the virtual screen.</summary>
    public DisplayBounds Bounds { get; private set; }

    /// <summary>
    /// Creates a hidden overlay window covering <paramref name="display"/>.
    /// </summary>
    public static OverlayWindow Create(DisplayInfo display, ILogger logger)
    {
        // The process is Per-Monitor v2 DPI aware (see app.manifest), so window
        // coordinates are physical pixels and match the enumerator's bounds
        // directly — no scaling conversion, which is exactly the bug that leaves
        // parts of a display uncovered in mixed-DPI setups.
        DisplayBounds bounds = display.Bounds;

        nint handle = User32.CreateWindowExW(
            ExtendedStyle,
            OverlayWindowClass.EnsureRegistered(),
            lpWindowName: null,
            User32.WsPopup,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            hWndParent: 0,
            hMenu: 0,
            NativeModule.Handle,
            lpParam: 0);

        if (handle == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not create the overlay window for {display.Id}.");
        }

        OverlayWindow window = new(handle, display.Id, bounds, logger);

        // Start fully transparent so the window can be created eagerly without
        // anything appearing on screen.
        User32.SetLayeredWindowAttributes(handle, Gdi32.Black, 0, User32.LwaAlpha);
        window.ExcludeFromScreenCapture();

        logger.LogDebug("Overlay window created for {DisplayId} at {Bounds}.", display.Id, bounds);
        return window;
    }

    /// <summary>Moves and resizes the window to follow a display change.</summary>
    public void SetBounds(DisplayBounds bounds)
    {
        if (bounds == Bounds)
        {
            return;
        }

        Bounds = bounds;

        if (_handle == 0)
        {
            return;
        }

        User32.SetWindowPos(
            _handle,
            User32.HwndTopmost,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            User32.SwpNoActivate);

        _logger.LogDebug("Overlay window for {DisplayId} moved to {Bounds}.", DisplayId, bounds);
    }

    /// <summary>
    /// Applies an alpha value. Zero hides the window rather than leaving a
    /// fully transparent surface for the compositor to blend for nothing.
    /// </summary>
    public void SetAlpha(byte alpha)
    {
        if (_handle == 0 || alpha == _alpha)
        {
            return;
        }

        _alpha = alpha;

        if (alpha == 0)
        {
            Hide();
            return;
        }

        User32.SetLayeredWindowAttributes(_handle, Gdi32.Black, alpha, User32.LwaAlpha);

        if (!_visible)
        {
            // Z-order is only touched when the window becomes visible, never on
            // a plain opacity change: re-raising on every slider tick would keep
            // yanking the overlay above the control panel the user is dragging.
            User32.SetWindowPos(
                _handle,
                User32.HwndTopmost,
                Bounds.X,
                Bounds.Y,
                Bounds.Width,
                Bounds.Height,
                User32.SwpNoActivate | User32.SwpShowWindow);

            _visible = true;
        }
    }

    private void Hide()
    {
        if (!_visible)
        {
            return;
        }

        User32.ShowWindow(_handle, User32.SwHide);
        _visible = false;
    }

    /// <summary>
    /// Asks Windows to leave this window out of screen captures, so the overlay
    /// dims the local screen without turning the picture black for everyone in
    /// a call or a recording.
    /// </summary>
    private void ExcludeFromScreenCapture()
    {
        if (User32.SetWindowDisplayAffinity(_handle, User32.WdaExcludeFromCapture))
        {
            return;
        }

        // Requires Windows 10 2004. On older builds the overlay simply shows up
        // in captures — worth a log line, not worth failing over.
        _logger.LogDebug(
            "Capture exclusion unavailable for the overlay on {DisplayId} (error {Error}); it will appear in screen captures.",
            DisplayId,
            Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (_handle == 0)
        {
            return;
        }

        User32.DestroyWindow(_handle);
        _handle = 0;
        _visible = false;

        _logger.LogDebug("Overlay window for {DisplayId} destroyed.", DisplayId);
    }
}
