using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Core.Color;
using Redture.Platform.Abstractions.Gamma;
using Redture.Platform.Linux.Interop;

namespace Redture.Platform.Linux.Gamma;

/// <inheritdoc cref="IGammaController" />
/// <remarks>
/// <para>
/// Writes a lookup table to every active CRTC through XRandR. Unlike GDI, which
/// accepts 256 entries and no other size, each CRTC declares its own — 256,
/// 1024 and 2048 all occur in the wild — so the ramp is built to whatever the
/// hardware asks for rather than to a constant.
/// </para>
/// <para>
/// There is no gamma range restriction here and no HDR equivalent to work
/// around: X11 either applies the table or the CRTC does not exist. That makes
/// this the simplest of the backends, which is a pleasant change.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class X11GammaController : IGammaController
{
    private readonly X11Connection _connection;
    private readonly ILogger<X11GammaController> _logger;

    private GammaRamp? _appliedRamp;
    private GammaRamp _requestedRamp = GammaRamp.Linear;
    private bool _disposed;

    public X11GammaController(X11Connection connection, ILogger<X11GammaController> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public bool IsSupported { get; private set; } = true;

    /// <summary>Always false: X11 imposes no limit on how far a ramp may deviate.</summary>
    public bool LastRampRejected => false;

    /// <summary>Always empty: X11 has no equivalent of the HDR no-op.</summary>
    public IReadOnlyList<string> DisplaysIgnoringGamma => [];

    public void Apply(GammaRamp ramp)
    {
        ArgumentNullException.ThrowIfNull(ramp);

        if (_disposed)
        {
            return;
        }

        _requestedRamp = ramp;

        if (_appliedRamp is not null && _appliedRamp.HasSameValues(ramp))
        {
            return;
        }

        Write(ramp);
    }

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        _appliedRamp = null;
        Write(_requestedRamp);
    }

    public void ResetToLinear()
    {
        if (_disposed)
        {
            return;
        }

        _requestedRamp = GammaRamp.Linear;
        _appliedRamp = null;
        Write(GammaRamp.Linear);
    }

    /// <summary>
    /// Reads each CRTC's table back and compares it with what was written.
    /// </summary>
    /// <remarks>
    /// The comparison is per CRTC and against a ramp rebuilt at that CRTC's own
    /// size, because sizes differ between outputs on the same machine. Same
    /// purpose as on Windows: catching another application writing the table,
    /// whatever that application happens to be.
    /// </remarks>
    public GammaVerification Verify()
    {
        if (_disposed || _appliedRamp is null)
        {
            return GammaVerification.Unknown;
        }

        GammaRamp expected = _appliedRamp;
        return _connection.Use((display, root) => VerifyAllCrtcs(display, root, expected), GammaVerification.Unknown);
    }

    private GammaVerification VerifyAllCrtcs(nint display, nuint root, GammaRamp expected)
    {
        nint resourcesPtr = XRandr.XRRGetScreenResourcesCurrent(display, root);
        if (resourcesPtr == 0)
        {
            return GammaVerification.Unknown;
        }

        try
        {
            XRRScreenResources resources = Marshal.PtrToStructure<XRRScreenResources>(resourcesPtr);

            if (resources.CrtcCount is < 0 or > 64 || resources.Crtcs == 0)
            {
                return GammaVerification.Unknown;
            }

            bool readAny = false;

            for (int i = 0; i < resources.CrtcCount; i++)
            {
                nuint crtc = (nuint)Marshal.ReadIntPtr(resources.Crtcs, i * nint.Size);

                switch (VerifyCrtc(display, crtc, expected))
                {
                    case GammaVerification.Foreign:
                        return GammaVerification.Foreign;
                    case GammaVerification.Matches:
                        readAny = true;
                        break;
                    default:
                        break;
                }
            }

            return readAny ? GammaVerification.Matches : GammaVerification.Unknown;
        }
        finally
        {
            XRandr.XRRFreeScreenResources(resourcesPtr);
        }
    }

    private GammaVerification VerifyCrtc(nint display, nuint crtc, GammaRamp expected)
    {
        int size = XRandr.XRRGetCrtcGammaSize(display, crtc);
        if (size <= 0)
        {
            return GammaVerification.Unknown;
        }

        nint gamma = XRandr.XRRGetCrtcGamma(display, crtc);
        if (gamma == 0)
        {
            return GammaVerification.Unknown;
        }

        try
        {
            XRRCrtcGamma header = Marshal.PtrToStructure<XRRCrtcGamma>(gamma);
            if (header.Size != size || header.Red == 0)
            {
                return GammaVerification.Unknown;
            }

            GammaRamp reference = expected.LevelsPerChannel == size ? expected : Rebuild(expected, size);

            return ChannelMatches(header.Red, reference, 0, size)
                && ChannelMatches(header.Green, reference, 1, size)
                && ChannelMatches(header.Blue, reference, 2, size)
                    ? GammaVerification.Matches
                    : GammaVerification.Foreign;
        }
        finally
        {
            XRandr.XRRFreeGamma(gamma);
        }
    }

    private static bool ChannelMatches(nint channel, GammaRamp reference, int channelIndex, int size)
    {
        short[] actual = new short[size];
        Marshal.Copy(channel, actual, 0, size);

        int offset = channelIndex * reference.LevelsPerChannel;

        for (int i = 0; i < size; i++)
        {
            if (unchecked((ushort)actual[i]) != reference.Values[offset + i])
            {
                return false;
            }
        }

        return true;
    }

    private void Write(GammaRamp ramp)
    {
        int applied = _connection.Use((display, root) => WriteToAllCrtcs(display, root, ramp), fallback: 0);

        IsSupported = applied > 0;
        _appliedRamp = applied > 0 ? ramp : null;
    }

    private int WriteToAllCrtcs(nint display, nuint root, GammaRamp ramp)
    {
        nint resourcesPtr = XRandr.XRRGetScreenResourcesCurrent(display, root);
        if (resourcesPtr == 0)
        {
            return 0;
        }

        try
        {
            XRRScreenResources resources = Marshal.PtrToStructure<XRRScreenResources>(resourcesPtr);

            if (resources.CrtcCount is < 0 or > 64 || resources.Crtcs == 0)
            {
                _logger.LogError(
                    "XRandR reported {Count} CRTCs, which cannot be right; refusing to write a gamma ramp.",
                    resources.CrtcCount);
                return 0;
            }

            int applied = 0;

            for (int i = 0; i < resources.CrtcCount; i++)
            {
                nuint crtc = (nuint)Marshal.ReadIntPtr(resources.Crtcs, i * nint.Size);

                if (WriteToCrtc(display, crtc, ramp))
                {
                    applied++;
                }
            }

            Xlib.XFlush(display);
            return applied;
        }
        finally
        {
            XRandr.XRRFreeScreenResources(resourcesPtr);
        }
    }

    private bool WriteToCrtc(nint display, nuint crtc, GammaRamp ramp)
    {
        int size = XRandr.XRRGetCrtcGammaSize(display, crtc);

        // Zero means this CRTC is not driving anything, which is the normal
        // state for the spare CRTCs every driver reports.
        if (size <= 0)
        {
            return false;
        }

        if (size > 65536)
        {
            _logger.LogError("A CRTC reported a gamma size of {Size}, which cannot be right.", size);
            return false;
        }

        // Rebuild at the size this CRTC wants rather than stretching the one we
        // were handed: a partially written table leaves the remainder holding
        // whatever it had before.
        GammaRamp sized = ramp.LevelsPerChannel == size
            ? ramp
            : Rebuild(ramp, size);

        nint gamma = XRandr.XRRAllocGamma(size);
        if (gamma == 0)
        {
            return false;
        }

        try
        {
            XRRCrtcGamma header = Marshal.PtrToStructure<XRRCrtcGamma>(gamma);

            if (header.Size != size || header.Red == 0 || header.Green == 0 || header.Blue == 0)
            {
                _logger.LogError("XRRAllocGamma returned a structure that does not match the requested size.");
                return false;
            }

            Marshal.Copy(ToSignedCopy(sized, 0), 0, header.Red, size);
            Marshal.Copy(ToSignedCopy(sized, 1), 0, header.Green, size);
            Marshal.Copy(ToSignedCopy(sized, 2), 0, header.Blue, size);

            XRandr.XRRSetCrtcGamma(display, crtc, gamma);
            return true;
        }
        finally
        {
            XRandr.XRRFreeGamma(gamma);
        }
    }

    private static GammaRamp Rebuild(GammaRamp source, int size)
    {
        // Recover the per-channel gains from the top entry, which is exactly
        // gain x MaxValue by construction, and rebuild at the required size.
        double red = source[0, source.LevelsPerChannel - 1] / (double)GammaRamp.MaxValue;
        double green = source[1, source.LevelsPerChannel - 1] / (double)GammaRamp.MaxValue;
        double blue = source[2, source.LevelsPerChannel - 1] / (double)GammaRamp.MaxValue;

        return GammaRamp.Create(red, green, blue, size);
    }

    /// <summary>
    /// Copies one channel into a <see cref="short"/> array, which is what
    /// <see cref="Marshal.Copy(short[], int, nint, int)"/> takes. The bit
    /// pattern is identical; only the sign interpretation differs, and the X
    /// server reads it back as unsigned.
    /// </summary>
    private static short[] ToSignedCopy(GammaRamp ramp, int channel)
    {
        short[] copy = new short[ramp.LevelsPerChannel];
        int offset = channel * ramp.LevelsPerChannel;

        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = unchecked((short)ramp.Values[offset + i]);
        }

        return copy;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ResetToLinear();
        _disposed = true;
        _logger.LogDebug("Gamma controller disposed; CRTCs restored to a linear ramp.");
    }
}
