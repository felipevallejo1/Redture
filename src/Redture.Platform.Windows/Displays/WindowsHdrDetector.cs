using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.Displays;

/// <inheritdoc cref="IHdrDetector" />
/// <remarks>
/// <para>
/// Walks the active display paths, resolves each one's GDI device name, and
/// asks it whether advanced colour is enabled.
/// </para>
/// <para>
/// Every failure path returns an empty set rather than throwing. Not knowing
/// whether a display is in HDR mode is a reason to carry on and let the user
/// judge by their eyes, not a reason to refuse to apply colour correction at
/// all.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsHdrDetector : IHdrDetector
{
    private readonly ILogger<WindowsHdrDetector> _logger;

    /// <summary>
    /// Whether the marshalled struct layouts match what the OS expects. Checked
    /// once: if they do not, the API is never called at all.
    /// </summary>
    private readonly bool _layoutsVerified;

    public WindowsHdrDetector(ILogger<WindowsHdrDetector> logger)
    {
        _logger = logger;
        _layoutsVerified = VerifyLayouts();
    }

    public IReadOnlySet<string> FindHdrDisplays()
    {
        HashSet<string> hdrDisplays = new(StringComparer.OrdinalIgnoreCase);

        if (!_layoutsVerified)
        {
            return hdrDisplays;
        }

        try
        {
            if (!TryQueryPaths(out DisplayConfigPathInfo[]? paths))
            {
                return hdrDisplays;
            }

            foreach (DisplayConfigPathInfo path in paths)
            {
                string? deviceName = TryGetGdiDeviceName(path);
                if (deviceName is null)
                {
                    continue;
                }

                bool answered = TryIsHdrEnabled(path, out bool hdrEnabled, out bool hdrCapable);

                // Logged unconditionally: this line is the only evidence that
                // the display-config structs are being marshalled correctly, and
                // a wrong layout would show up here as a garbled device name
                // long before it showed up as a wrong answer.
                _logger.LogDebug(
                    "Display path {DisplayId}: HDR capable={Capable} enabled={Enabled} (answered={Answered}).",
                    deviceName,
                    hdrCapable,
                    hdrEnabled,
                    answered);

                if (answered && hdrEnabled)
                {
                    _logger.LogInformation(
                        "{DisplayId} is running in HDR mode; gamma ramps are ignored there, so colour temperature cannot be applied to it.",
                        deviceName);

                    hdrDisplays.Add(deviceName);
                }
            }

            _logger.LogDebug(
                "Examined {Paths} display path(s); HDR active on {HdrCount}.",
                paths.Length,
                hdrDisplays.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine the HDR state of the attached displays.");
        }

        return hdrDisplays;
    }

    /// <summary>
    /// Confirms the P/Invoke structs marshal to the sizes the OS writes.
    /// </summary>
    /// <remarks>
    /// <c>QueryDisplayConfig</c> fills caller-allocated arrays using the native
    /// element size. If a declaration here were a few bytes short, the OS would
    /// write past the end of a managed array — silent heap corruption, not an
    /// error code. One equality check per size turns that into a clean refusal
    /// to answer.
    /// </remarks>
    private bool VerifyLayouts()
    {
        (string Name, int Actual, int Expected)[] checks =
        [
            ("DISPLAYCONFIG_PATH_INFO", Marshal.SizeOf<DisplayConfigPathInfo>(), DisplayConfig.PathInfoSize),
            ("DISPLAYCONFIG_MODE_INFO", Marshal.SizeOf<DisplayConfigModeInfo>(), DisplayConfig.ModeInfoSize),
            ("DISPLAYCONFIG_SOURCE_DEVICE_NAME", Marshal.SizeOf<DisplayConfigSourceDeviceName>(), DisplayConfig.SourceDeviceNameSize),
            ("DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO", Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo>(), DisplayConfig.AdvancedColorInfoSize),
            ("DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2", Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo2>(), DisplayConfig.AdvancedColorInfo2Size),
        ];

        foreach ((string name, int actual, int expected) in checks)
        {
            if (actual != expected)
            {
                _logger.LogError(
                    "{Struct} marshals to {Actual} bytes but the API expects {Expected}; HDR detection is disabled to avoid corrupting memory.",
                    name,
                    actual,
                    expected);
                return false;
            }
        }

        return true;
    }

    private bool TryQueryPaths(out DisplayConfigPathInfo[] paths)
    {
        paths = [];

        int result = DisplayConfig.GetDisplayConfigBufferSizes(
            DisplayConfig.OnlyActivePaths,
            out uint pathCount,
            out uint modeCount);

        if (result != DisplayConfig.ErrorSuccess || pathCount == 0)
        {
            _logger.LogDebug("GetDisplayConfigBufferSizes returned {Result} with {Paths} path(s).", result, pathCount);
            return false;
        }

        DisplayConfigPathInfo[] pathArray = new DisplayConfigPathInfo[pathCount];
        DisplayConfigModeInfo[] modeArray = new DisplayConfigModeInfo[modeCount];

        result = DisplayConfig.QueryDisplayConfig(
            DisplayConfig.OnlyActivePaths,
            ref pathCount,
            pathArray,
            ref modeCount,
            modeArray,
            currentTopologyId: 0);

        if (result != DisplayConfig.ErrorSuccess)
        {
            _logger.LogDebug("QueryDisplayConfig returned {Result}.", result);
            return false;
        }

        // The call can report back fewer paths than the buffer held.
        paths = pathCount < pathArray.Length ? pathArray[..(int)pathCount] : pathArray;

        if (!AdapterIdsAreConsistent(paths))
        {
            paths = [];
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks an invariant the API guarantees: a display path connects a source
    /// and a target on the <em>same</em> adapter.
    /// </summary>
    /// <remarks>
    /// This is the check that actually catches a wrong struct layout. Comparing
    /// <c>Marshal.SizeOf</c> against a constant only proves the declaration
    /// matches the number next to it, and both can be wrong together — which is
    /// precisely what happened here, when a <c>statusFlags</c> field was left
    /// out of the source-info struct and every following member shifted by four
    /// bytes. Reading a value the OS is known to have written, and checking it
    /// against something that must be true, is worth more than any number of
    /// size assertions.
    /// </remarks>
    private bool AdapterIdsAreConsistent(DisplayConfigPathInfo[] paths)
    {
        foreach (DisplayConfigPathInfo path in paths)
        {
            if (path.SourceInfo.AdapterId.LowPart == path.TargetInfo.AdapterId.LowPart
                && path.SourceInfo.AdapterId.HighPart == path.TargetInfo.AdapterId.HighPart)
            {
                continue;
            }

            _logger.LogError(
                "A display path reports different adapters for its source and target, which cannot happen. The display-config structs are being marshalled incorrectly, so HDR detection is disabled.");
            return false;
        }

        return true;
    }

    /// <summary>Resolves a path's source to its <c>\\.\DISPLAYn</c> name.</summary>
    private string? TryGetGdiDeviceName(DisplayConfigPathInfo path)
    {
        DisplayConfigSourceDeviceName request = new()
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfig.DeviceInfoGetSourceName,
                Size = (uint)DisplayConfig.SourceDeviceNameSize,
                AdapterId = path.SourceInfo.AdapterId,
                Id = path.SourceInfo.Id,
            },
        };

        int result = DisplayConfig.DisplayConfigGetDeviceInfo(ref request);
        if (result != DisplayConfig.ErrorSuccess || string.IsNullOrWhiteSpace(request.ViewGdiDeviceName))
        {
            return null;
        }

        return request.ViewGdiDeviceName;
    }

    /// <summary>
    /// Asks a path's target whether it is currently in HDR mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two queries exist. The original is asked first because it is the one
    /// verified to work: on Windows 11 build 10.0.26200 it answers correctly,
    /// while <c>GET_ADVANCED_COLOR_INFO_2</c> returns
    /// <c>ERROR_INVALID_PARAMETER</c> on the same machine despite being the
    /// documented replacement.
    /// </para>
    /// <para>
    /// The newer query is kept as a fallback for builds that eventually drop
    /// the original. It is <em>unverified</em> — it has never returned success
    /// here, so its struct layout has never actually been exercised. It fails
    /// safe: the OS validates the size field in the request header and rejects
    /// a mismatched packet rather than filling it with nonsense.
    /// </para>
    /// </remarks>
    private bool TryIsHdrEnabled(DisplayConfigPathInfo path, out bool enabled, out bool capable)
    {
        if (TryQueryAdvancedColorLegacy(path, out enabled, out capable))
        {
            return true;
        }

        return TryQueryAdvancedColor2(path, out enabled, out capable);
    }

    private bool TryQueryAdvancedColor2(DisplayConfigPathInfo path, out bool enabled, out bool capable)
    {
        enabled = false;
        capable = false;

        DisplayConfigGetAdvancedColorInfo2 request = new()
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfig.DeviceInfoGetAdvancedColorInfo2,
                Size = (uint)DisplayConfig.AdvancedColorInfo2Size,
                AdapterId = path.TargetInfo.AdapterId,
                Id = path.TargetInfo.Id,
            },
        };

        int result = DisplayConfig.DisplayConfigGetDeviceInfo(ref request);
        if (result != DisplayConfig.ErrorSuccess)
        {
            _logger.LogDebug("GET_ADVANCED_COLOR_INFO_2 returned {Result}.", result);
            return false;
        }

        enabled = request.IsHdrActive;
        capable = request.IsHdrSupported;
        return true;
    }

    private bool TryQueryAdvancedColorLegacy(DisplayConfigPathInfo path, out bool enabled, out bool capable)
    {
        enabled = false;
        capable = false;

        DisplayConfigGetAdvancedColorInfo request = new()
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfig.DeviceInfoGetAdvancedColorInfo,
                Size = (uint)DisplayConfig.AdvancedColorInfoSize,
                AdapterId = path.TargetInfo.AdapterId,
                Id = path.TargetInfo.Id,
            },
        };

        int result = DisplayConfig.DisplayConfigGetDeviceInfo(ref request);
        if (result != DisplayConfig.ErrorSuccess)
        {
            _logger.LogDebug("GET_ADVANCED_COLOR_INFO returned {Result}; trying the newer query.", result);
            return false;
        }

        enabled = request.AdvancedColorEnabled;
        capable = request.AdvancedColorSupported;
        return true;
    }
}
