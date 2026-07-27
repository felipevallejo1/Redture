using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.MacOS.Interop;

namespace Redture.Platform.MacOS.Displays;

/// <inheritdoc cref="IDisplayEnumerator" />
/// <remarks>
/// <b>Unverified against a real Mac.</b> See <see cref="CoreGraphics"/>.
/// </remarks>
[SupportedOSPlatform("macos")]
public sealed class MacDisplayEnumerator : IDisplayEnumerator
{
    private readonly ILogger<MacDisplayEnumerator> _logger;

    private string _lastLoggedSignature = string.Empty;

    public MacDisplayEnumerator(ILogger<MacDisplayEnumerator> logger) => _logger = logger;

    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        try
        {
            uint[] ids = new uint[CoreGraphics.MaxDisplays];

            if (CoreGraphics.CGGetActiveDisplayList(CoreGraphics.MaxDisplays, ids, out uint count) != CoreGraphics.Success)
            {
                _logger.LogWarning("CGGetActiveDisplayList failed.");
                return [];
            }

            count = Math.Min(count, CoreGraphics.MaxDisplays);
            List<DisplayInfo> displays = [];

            for (uint i = 0; i < count; i++)
            {
                uint id = ids[i];
                CGRect bounds = CoreGraphics.CGDisplayBounds(id);

                if (bounds.Size.Width <= 0 || bounds.Size.Height <= 0)
                {
                    continue;
                }

                displays.Add(new DisplayInfo(
                    Id: id.ToString(System.Globalization.CultureInfo.InvariantCulture),

                    // Core Graphics has no friendly name; getting one means
                    // going through IOKit for the EDID. The display id is at
                    // least stable and unambiguous, which is what the rest of
                    // the application actually needs it for.
                    Name: $"Display {id}",
                    Bounds: new DisplayBounds(
                        (int)bounds.Origin.X,
                        (int)bounds.Origin.Y,
                        (int)bounds.Size.Width,
                        (int)bounds.Size.Height),
                    IsPrimary: CoreGraphics.CGDisplayIsMain(id) != 0,

                    // Bounds are already in points rather than pixels, so the
                    // Retina factor is folded in and reporting it again would
                    // double-count it.
                    ScaleFactor: 1d));
            }

            LogIfChanged(displays);
            return displays;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _logger.LogWarning(ex, "Core Graphics is not available; no displays can be enumerated.");
            return [];
        }
    }

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
