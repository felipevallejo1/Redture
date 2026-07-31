using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Core.Color;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Gamma;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.Gamma;

/// <inheritdoc cref="IGammaController" />
/// <remarks>
/// <para>
/// Device contexts are opened and closed around each write rather than being
/// held open. A cached <c>HDC</c> would have to be invalidated on every
/// topology change, resolution change, undock and remote-desktop transition —
/// a stale one silently writes to nothing. <c>CreateDC</c> costs microseconds
/// against a write path that runs at most a few times a second, so the
/// bookkeeping buys nothing and can only introduce bugs.
/// </para>
/// <para>
/// Redture restores the identity ramp rather than whatever ramp it found at
/// startup. Reading first would look more polite, but if another colour tool
/// was already running its tint would be captured as "the original" and
/// faithfully restored forever after.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsGammaController : IGammaController
{
    private readonly IDisplayEnumerator _displayEnumerator;
    private readonly IHdrDetector _hdrDetector;
    private readonly ILogger<WindowsGammaController> _logger;

    /// <summary>
    /// Displays where a ramp would be accepted and then ignored. Cached rather
    /// than queried per write: the state only changes when the display
    /// configuration does, which is exactly when <see cref="Refresh"/> runs.
    /// </summary>
    private IReadOnlySet<string> _hdrDisplays = new HashSet<string>();

    private bool _hdrChecked;

    /// <summary>
    /// Ramp currently believed to be loaded, or null when that belief is not
    /// safe to hold — before the first write, or after Windows has reset the
    /// LUT behind our back.
    /// </summary>
    private GammaRamp? _appliedRamp;

    /// <summary>Last ramp requested, re-sent by <see cref="Refresh"/>.</summary>
    private GammaRamp _requestedRamp = GammaRamp.Linear;

    private bool _disposed;

    public WindowsGammaController(
        IDisplayEnumerator displayEnumerator,
        IHdrDetector hdrDetector,
        ILogger<WindowsGammaController> logger)
    {
        _displayEnumerator = displayEnumerator;
        _hdrDetector = hdrDetector;
        _logger = logger;
    }

    public bool IsSupported { get; private set; } = true;

    public bool LastRampRejected { get; private set; }

    /// <summary>
    /// Displays that are in HDR mode, where a gamma ramp is accepted and then
    /// silently ignored.
    /// </summary>
    public IReadOnlyList<string> DisplaysIgnoringGamma { get; private set; } = [];

    public void Apply(GammaRamp ramp)
    {
        ArgumentNullException.ThrowIfNull(ramp);

        if (_disposed)
        {
            return;
        }

        _requestedRamp = ramp;

        // The flicker guard: a transition passes through the same rounded table
        // repeatedly, and re-sending it is what makes these tools stutter.
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

        // Forget what the driver is believed to hold: Windows drops the LUT on
        // a session unlock or a secure-desktop prompt without telling anyone,
        // so the cached value is exactly what must not be trusted here.
        _appliedRamp = null;

        // HDR can be toggled per display at any time, and the only signals that
        // it happened are the same ones that bring us here.
        _hdrChecked = false;

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

    public GammaVerification Verify()
    {
        if (_disposed || _appliedRamp is null)
        {
            return GammaVerification.Unknown;
        }

        // GDI's ramp is always 256 entries per channel; it accepts no other size.
        ushort[] readBack = new ushort[GammaRamp.Channels * GammaRamp.DefaultLevelsPerChannel];
        bool readAny = false;

        foreach (DisplayInfo display in _displayEnumerator.GetDisplays())
        {
            nint deviceContext = Gdi32.CreateDCW("DISPLAY", display.Id, null, 0);
            if (deviceContext == 0)
            {
                continue;
            }

            bool read;
            try
            {
                read = Gdi32.GetDeviceGammaRamp(deviceContext, readBack);
            }
            finally
            {
                Gdi32.DeleteDC(deviceContext);
            }

            if (!read)
            {
                continue;
            }

            readAny = true;

            if (!readBack.AsSpan().SequenceEqual(_appliedRamp.Values))
            {
                _logger.LogDebug("The LUT on {DisplayId} is no longer the one Redture wrote.", display.Id);
                return GammaVerification.Foreign;
            }
        }

        return readAny ? GammaVerification.Matches : GammaVerification.Unknown;
    }

    private void Write(GammaRamp ramp)
    {
        EnsureHdrStateKnown();

        IReadOnlyList<DisplayInfo> displays = _displayEnumerator.GetDisplays();

        int applied = 0;
        int rejected = 0;
        int ignored = 0;
        List<string> hdrDisplays = [];

        foreach (DisplayInfo display in displays)
        {
            if (_hdrDisplays.Contains(display.Id))
            {
                // Writing here would report success and change nothing. Skipping
                // it keeps the "did this work" bookkeeping honest.
                ignored++;
                hdrDisplays.Add(display.Name);
                continue;
            }

            if (TryWriteTo(display, ramp))
            {
                applied++;
            }
            else
            {
                rejected++;
            }
        }

        DisplaysIgnoringGamma = hdrDisplays;
        LastRampRejected = rejected > 0;
        IsSupported = applied > 0 || displays.Count == ignored;

        // Only claim the driver holds this ramp if it actually took everywhere;
        // a partial write must be retried, not cached.
        _appliedRamp = rejected == 0 && applied > 0 ? ramp : null;

        if (ignored > 0)
        {
            _logger.LogDebug(
                "{Ignored} display(s) skipped because they are in HDR mode, where gamma ramps have no effect.",
                ignored);
        }

        if (rejected > 0)
        {
            _logger.LogWarning(
                "{Rejected} of {Total} display(s) refused the gamma ramp. Windows restricts how far a ramp may deviate from linear; strongly warm settings need the extended range opt-in.",
                rejected,
                displays.Count);
        }
    }

    /// <summary>
    /// Populates the HDR display set on first use, and after anything that
    /// could have changed it.
    /// </summary>
    private void EnsureHdrStateKnown()
    {
        if (_hdrChecked)
        {
            return;
        }

        _hdrChecked = true;
        _hdrDisplays = _hdrDetector.FindHdrDisplays();
    }

    /// <summary>
    /// Fractions of the requested correction to fall back through when Windows
    /// refuses the full one.
    /// </summary>
    /// <remarks>
    /// Without this the slider simply stops working below some temperature: the
    /// ramp is refused outright and the screen keeps whatever it had, so the
    /// control moves and nothing happens. Stepping down until something is
    /// accepted gives the user the warmest setting the machine actually allows,
    /// which is a far better answer than none. The panel still offers the
    /// registry change for the full range.
    /// </remarks>
    private static readonly double[] FallbackScales = [0.8d, 0.6d, 0.45d, 0.3d, 0.2d];

    /// <summary>
    /// Fraction of the correction last accepted, used as the starting point
    /// next time so the search is not repeated on every write.
    /// </summary>
    private double _lastAcceptedScale = 1d;

    /// <summary>
    /// Blends a ramp towards the identity, so a milder version of the same
    /// correction can be offered when the full one is refused.
    /// </summary>
    private static GammaRamp Soften(GammaRamp ramp, double scale)
    {
        GammaRamp linear = GammaRamp.LinearWithSize(ramp.LevelsPerChannel);
        ushort[] blended = new ushort[ramp.Values.Length];

        for (int i = 0; i < blended.Length; i++)
        {
            double value = linear.Values[i] + ((ramp.Values[i] - linear.Values[i]) * scale);
            blended[i] = (ushort)Math.Clamp(Math.Round(value), 0d, GammaRamp.MaxValue);
        }

        return GammaRamp.FromValues(blended, ramp.LevelsPerChannel);
    }

    /// <summary>
    /// Writes the strongest version of a ramp the display will take.
    /// </summary>
    /// <remarks>
    /// Tries the full correction, and on refusal works down through
    /// <see cref="FallbackScales"/> until something is accepted. The scale that
    /// worked is remembered and tried first next time, so a restricted machine
    /// pays for the search once rather than on every slider movement.
    /// </remarks>
    private bool TryWriteTo(DisplayInfo display, GammaRamp ramp)
    {
        nint deviceContext = Gdi32.CreateDCW("DISPLAY", display.Id, null, 0);
        if (deviceContext == 0)
        {
            _logger.LogWarning(
                "Could not open a device context for {DisplayId} (error {Error}).",
                display.Id,
                Marshal.GetLastWin32Error());
            return false;
        }

        try
        {
            if (Gdi32.SetDeviceGammaRamp(deviceContext, ramp.Values))
            {
                _lastAcceptedScale = 1d;
                return true;
            }

            foreach (double scale in FallbackScales)
            {
                if (Gdi32.SetDeviceGammaRamp(deviceContext, Soften(ramp, scale).Values))
                {
                    if (scale != _lastAcceptedScale)
                    {
                        _logger.LogInformation(
                            "Windows refused the full correction on {DisplayId}; applied {Percent:0}% of it instead. "
                            + "The extended gamma range unlocks the rest.",
                            display.Id,
                            scale * 100d);
                    }

                    _lastAcceptedScale = scale;

                    // Reported as a rejection even though something was applied,
                    // so the panel keeps offering the registry change: the user
                    // asked for a warmth they are not getting.
                    return false;
                }
            }

            _logger.LogDebug(
                "SetDeviceGammaRamp was refused for {DisplayId} at every strength (error {Error}).",
                display.Id,
                Marshal.GetLastWin32Error());
            return false;
        }
        finally
        {
            Gdi32.DeleteDC(deviceContext);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Hand the displays back before going away. The LUT is global driver
        // state: anything left here stays on screen with no process left to
        // explain it.
        ResetToLinear();

        _disposed = true;
        _logger.LogDebug("Gamma controller disposed; displays restored to a linear ramp.");
    }
}
