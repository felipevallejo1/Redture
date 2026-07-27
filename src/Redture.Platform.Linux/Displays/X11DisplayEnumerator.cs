using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Linux.Interop;

namespace Redture.Platform.Linux.Displays;

/// <inheritdoc cref="IDisplayEnumerator" />
/// <remarks>
/// Walks the XRandR outputs, keeps the ones with something plugged in and a
/// CRTC driving them, and reads the geometry from the CRTC.
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class X11DisplayEnumerator : IDisplayEnumerator
{
    /// <summary>
    /// Ceiling used to sanity-check the counts read out of the server's
    /// structures.
    /// </summary>
    /// <remarks>
    /// A struct layout that does not match the C header shows up as a wildly
    /// implausible count long before it shows up as anything else. Checking a
    /// value the server is known to have written, against something that must
    /// be true, is the cheapest guard there is against reading a pointer array
    /// at the wrong offset.
    /// </remarks>
    private const int MaxPlausibleCount = 64;

    private readonly X11Connection _connection;
    private readonly ILogger<X11DisplayEnumerator> _logger;

    private string _lastLoggedSignature = string.Empty;

    public X11DisplayEnumerator(X11Connection connection, ILogger<X11DisplayEnumerator> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public IReadOnlyList<DisplayInfo> GetDisplays() =>
        _connection.Use(Enumerate, fallback: (IReadOnlyList<DisplayInfo>)[]);

    private IReadOnlyList<DisplayInfo> Enumerate(nint display, nuint root)
    {
        nint resourcesPtr = XRandr.XRRGetScreenResourcesCurrent(display, root);
        if (resourcesPtr == 0)
        {
            _logger.LogWarning("XRandR returned no screen resources.");
            return [];
        }

        try
        {
            XRRScreenResources resources = Marshal.PtrToStructure<XRRScreenResources>(resourcesPtr);

            if (resources.OutputCount is < 0 or > MaxPlausibleCount || resources.Outputs == 0)
            {
                _logger.LogError(
                    "XRandR reported {Count} outputs, which cannot be right; the interop structs do not match this libXrandr.",
                    resources.OutputCount);
                return [];
            }

            List<DisplayInfo> displays = [];
            bool first = true;

            for (int i = 0; i < resources.OutputCount; i++)
            {
                nuint output = ReadHandle(resources.Outputs, i);

                if (Describe(display, resourcesPtr, output, isPrimaryCandidate: first) is { } info)
                {
                    displays.Add(info);
                    first = false;
                }
            }

            LogIfChanged(displays);
            return displays;
        }
        finally
        {
            XRandr.XRRFreeScreenResources(resourcesPtr);
        }
    }

    private DisplayInfo? Describe(nint display, nint resources, nuint output, bool isPrimaryCandidate)
    {
        nint outputPtr = XRandr.XRRGetOutputInfo(display, resources, output);
        if (outputPtr == 0)
        {
            return null;
        }

        try
        {
            XRROutputInfo info = Marshal.PtrToStructure<XRROutputInfo>(outputPtr);

            // Nothing plugged in, or plugged in but not being driven: either
            // way there is no surface to correct.
            if (info.Connection != XRandr.Connected || info.Crtc == 0)
            {
                return null;
            }

            string name = ReadName(info);
            nint crtcPtr = XRandr.XRRGetCrtcInfo(display, resources, info.Crtc);

            if (crtcPtr == 0)
            {
                return null;
            }

            try
            {
                XRRCrtcInfo crtc = Marshal.PtrToStructure<XRRCrtcInfo>(crtcPtr);

                if (crtc.Width is 0 or > 65536 || crtc.Height is 0 or > 65536)
                {
                    _logger.LogError(
                        "CRTC for {Output} reports {Width}x{Height}, which cannot be right; the interop structs do not match this libXrandr.",
                        name,
                        crtc.Width,
                        crtc.Height);
                    return null;
                }

                return new DisplayInfo(
                    Id: name,
                    Name: name,
                    Bounds: new DisplayBounds(crtc.X, crtc.Y, (int)crtc.Width, (int)crtc.Height),
                    IsPrimary: isPrimaryCandidate,

                    // X11 has no per-monitor scale factor. Toolkits derive one
                    // from Xft.dpi or their own settings, and reporting an
                    // invented number here would be worse than reporting none.
                    ScaleFactor: 1d);
            }
            finally
            {
                XRandr.XRRFreeCrtcInfo(crtcPtr);
            }
        }
        finally
        {
            XRandr.XRRFreeOutputInfo(outputPtr);
        }
    }

    /// <summary>
    /// Reads an output's name, which is not null-terminated: the length comes
    /// from the struct.
    /// </summary>
    private static string ReadName(XRROutputInfo info)
    {
        if (info.Name == 0 || info.NameLength is <= 0 or > 256)
        {
            return "Unknown output";
        }

        byte[] buffer = new byte[info.NameLength];
        Marshal.Copy(info.Name, buffer, 0, info.NameLength);
        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>Reads the i-th XID out of a native array of machine words.</summary>
    private static nuint ReadHandle(nint array, int index) =>
        (nuint)Marshal.ReadIntPtr(array, index * nint.Size);

    private void LogIfChanged(List<DisplayInfo> displays)
    {
        string signature = string.Join(" | ", displays);
        if (signature == _lastLoggedSignature)
        {
            return;
        }

        _lastLoggedSignature = signature;
        _logger.LogInformation("Detected {Count} display(s): {Displays}", displays.Count, signature);
    }
}
