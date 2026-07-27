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
    private readonly ILogger<WindowsGammaController> _logger;

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
        ILogger<WindowsGammaController> logger)
    {
        _displayEnumerator = displayEnumerator;
        _logger = logger;
    }

    public bool IsSupported { get; private set; } = true;

    public bool LastRampRejected { get; private set; }

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

        ushort[] readBack = new ushort[GammaRamp.Channels * GammaRamp.LevelsPerChannel];
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
        IReadOnlyList<DisplayInfo> displays = _displayEnumerator.GetDisplays();

        int applied = 0;
        int rejected = 0;

        foreach (DisplayInfo display in displays)
        {
            if (TryWriteTo(display, ramp))
            {
                applied++;
            }
            else
            {
                rejected++;
            }
        }

        LastRampRejected = rejected > 0;
        IsSupported = applied > 0 || displays.Count == 0;

        // Only claim the driver holds this ramp if it actually took everywhere;
        // a partial write must be retried, not cached.
        _appliedRamp = rejected == 0 && applied > 0 ? ramp : null;

        if (rejected > 0)
        {
            _logger.LogWarning(
                "{Rejected} of {Total} display(s) refused the gamma ramp. Windows restricts how far a ramp may deviate from linear; strongly warm settings need the extended range opt-in.",
                rejected,
                displays.Count);
        }
    }

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
                return true;
            }

            _logger.LogDebug(
                "SetDeviceGammaRamp was refused for {DisplayId} (error {Error}).",
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
