using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Core.Color;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Gamma;
using Redture.Platform.MacOS.Interop;

namespace Redture.Platform.MacOS.Gamma;

/// <inheritdoc cref="IGammaController" />
/// <remarks>
/// <para>
/// <b>Unverified against a real Mac.</b> See <see cref="CoreGraphics"/>.
/// </para>
/// <para>
/// Two things differ from the other backends. Core Graphics takes floats in
/// [0, 1] rather than 16-bit integers, so the ramp is converted on the way out.
/// And restoring means asking the system to reapply the display's colour
/// profile rather than writing an identity ramp: on a calibrated Mac those are
/// not the same, and overwriting somebody's calibration with a straight line
/// would be a rude way to exit.
/// </para>
/// </remarks>
[SupportedOSPlatform("macos")]
public sealed class MacGammaController : IGammaController
{
    private readonly IDisplayEnumerator _displayEnumerator;
    private readonly ILogger<MacGammaController> _logger;

    private GammaRamp? _appliedRamp;
    private GammaRamp _requestedRamp = GammaRamp.Linear;
    private bool _disposed;

    public MacGammaController(IDisplayEnumerator displayEnumerator, ILogger<MacGammaController> logger)
    {
        _displayEnumerator = displayEnumerator;
        _logger = logger;
    }

    public bool IsSupported { get; private set; } = true;

    /// <summary>Core Graphics imposes no equivalent of Windows' gamma range limit.</summary>
    public bool LastRampRejected { get; private set; }

    /// <summary>
    /// Always empty. macOS applies the table under HDR as well, so there is no
    /// silent no-op to warn about.
    /// </summary>
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

        try
        {
            CoreGraphics.CGDisplayRestoreColorSyncSettings();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _logger.LogWarning(ex, "Could not restore the display colour profile.");
        }
    }

    /// <summary>
    /// Not implemented. Core Graphics can read the table back, but comparing it
    /// requires resampling both sides onto a common grid, and shipping that
    /// untested would be worse than admitting the answer is unknown.
    /// </summary>
    public GammaVerification Verify() => GammaVerification.Unknown;

    private void Write(GammaRamp ramp)
    {
        int applied = 0;
        LastRampRejected = false;

        try
        {
            foreach (DisplayInfo display in _displayEnumerator.GetDisplays())
            {
                if (!uint.TryParse(display.Id, out uint id))
                {
                    continue;
                }

                if (WriteToDisplay(id, ramp))
                {
                    applied++;
                }
                else
                {
                    LastRampRejected = true;
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _logger.LogWarning(ex, "Core Graphics is not available; colour temperature is unsupported.");
            IsSupported = false;
            return;
        }

        IsSupported = applied > 0;
        _appliedRamp = applied > 0 && !LastRampRejected ? ramp : null;
    }

    private bool WriteToDisplay(uint id, GammaRamp ramp)
    {
        uint capacity = CoreGraphics.CGDisplayGammaTableCapacity(id);
        if (capacity == 0)
        {
            return false;
        }

        int size = (int)Math.Min(capacity, 4096);

        // Built at the size this display accepts rather than stretched, for the
        // same reason as on X11: a partially written table leaves the remainder
        // holding whatever it had before.
        GammaRamp sized = ramp.LevelsPerChannel == size ? ramp : Rebuild(ramp, size);

        float[] red = ToFloats(sized, 0, size);
        float[] green = ToFloats(sized, 1, size);
        float[] blue = ToFloats(sized, 2, size);

        int result = CoreGraphics.CGSetDisplayTransferByTable(id, (uint)size, red, green, blue);

        if (result == CoreGraphics.Success)
        {
            return true;
        }

        _logger.LogWarning("CGSetDisplayTransferByTable returned {Result} for display {Display}.", result, id);
        return false;
    }

    private static GammaRamp Rebuild(GammaRamp source, int size)
    {
        double red = source[0, source.LevelsPerChannel - 1] / (double)GammaRamp.MaxValue;
        double green = source[1, source.LevelsPerChannel - 1] / (double)GammaRamp.MaxValue;
        double blue = source[2, source.LevelsPerChannel - 1] / (double)GammaRamp.MaxValue;

        return GammaRamp.Create(red, green, blue, size);
    }

    /// <summary>Converts one channel from 16-bit integers to normalised floats.</summary>
    private static float[] ToFloats(GammaRamp ramp, int channel, int size)
    {
        float[] values = new float[size];
        int offset = channel * ramp.LevelsPerChannel;

        for (int i = 0; i < size; i++)
        {
            values[i] = ramp.Values[offset + i] / (float)GammaRamp.MaxValue;
        }

        return values;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ResetToLinear();
        _disposed = true;
        _logger.LogDebug("Gamma controller disposed; displays restored to their colour profiles.");
    }
}
