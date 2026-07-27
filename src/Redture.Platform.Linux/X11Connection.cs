using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Linux.Interop;

namespace Redture.Platform.Linux;

/// <summary>
/// Owns the process's connection to the X server.
/// </summary>
/// <remarks>
/// <para>
/// One connection, shared by everything that needs it, with a lock around every
/// use. Xlib is only thread-safe after <c>XInitThreads</c>, and even then
/// interleaving request sequences from several threads on one connection is a
/// reliable way to desynchronise the protocol stream. Serialising is cheap:
/// nothing here runs more than a few times a second.
/// </para>
/// <para>
/// A failure to connect is an ordinary outcome, not an error. Redture runs
/// under Wayland, over SSH without forwarding, and on machines with no display
/// at all; in each case the answer is to report that nothing can be adjusted
/// and carry on.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class X11Connection : IDisposable
{
    private readonly Lock _gate = new();
    private readonly ILogger _logger;

    private nint _display;
    private bool _disposed;

    private X11Connection(nint display, nuint rootWindow, ILogger logger)
    {
        _display = display;
        RootWindow = rootWindow;
        _logger = logger;
    }

    /// <summary>The root window of the default screen.</summary>
    public nuint RootWindow { get; }

    /// <summary>
    /// Whether there is actually a server on the other end. False is an
    /// ordinary answer, not a failure.
    /// </summary>
    public bool IsConnected => _display != 0;

    /// <summary>
    /// Connects to the display named by <c>$DISPLAY</c>. Always returns an
    /// instance; check <see cref="IsConnected"/>.
    /// </summary>
    /// <remarks>
    /// Never null, so that dependency injection never has to express "maybe a
    /// connection". A disconnected instance answers every request with the
    /// caller's fallback, which is exactly what a caller would have had to do
    /// with a null anyway.
    /// </remarks>
    public static X11Connection Open(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        string? displayName = Environment.GetEnvironmentVariable("DISPLAY");
        if (string.IsNullOrEmpty(displayName))
        {
            logger.LogInformation(
                "DISPLAY is not set, so there is no X server to talk to. Under Wayland this is expected.");
            return new X11Connection(0, 0, logger);
        }

        try
        {
            // Must precede every other Xlib call in the process.
            Xlib.XInitThreads();

            nint display = Xlib.XOpenDisplay(null);
            if (display == 0)
            {
                logger.LogWarning("Could not connect to the X server at {Display}.", displayName);
                return new X11Connection(0, 0, logger);
            }

            nuint root = (nuint)Xlib.XDefaultRootWindow(display);
            logger.LogInformation("Connected to the X server at {Display}.", displayName);

            return new X11Connection(display, root, logger);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // libX11 absent or incomplete: a headless container, or a
            // Wayland-only install without the compatibility libraries.
            logger.LogInformation("libX11 is not usable on this system ({Message}).", ex.Message);
            return new X11Connection(0, 0, logger);
        }
    }

    /// <summary>
    /// Runs an operation against the connection, holding the lock for its
    /// duration.
    /// </summary>
    public T Use<T>(Func<nint, nuint, T> operation, T fallback)
    {
        ArgumentNullException.ThrowIfNull(operation);

        lock (_gate)
        {
            if (_disposed || _display == 0)
            {
                return fallback;
            }

            return operation(_display, RootWindow);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_display != 0)
            {
                Xlib.XCloseDisplay(_display);
                _display = 0;
            }
        }

        _logger.LogDebug("X server connection closed.");
    }
}
