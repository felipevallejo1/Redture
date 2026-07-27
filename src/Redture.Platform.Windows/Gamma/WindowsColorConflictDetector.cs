using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Gamma;

namespace Redture.Platform.Windows.Gamma;

/// <inheritdoc cref="IColorConflictDetector" />
[SupportedOSPlatform("windows")]
public sealed class WindowsColorConflictDetector : IColorConflictDetector
{
    /// <summary>
    /// Process names, without extension, mapped to what a human calls them.
    /// The list is a convenience, not the detection mechanism: missing an entry
    /// costs a friendly name, never a missed conflict.
    /// </summary>
    private static readonly (string Process, string DisplayName)[] KnownApplications =
    [
        ("flux", "f.lux"),
        ("iris", "Iris"),
        ("irisservice", "Iris"),
        ("lightbulb", "LightBulb"),
        ("sunsetscreen", "SunsetScreen"),
        ("redshift", "Redshift"),
        ("gammy", "Gammy"),
        ("clickmonitorddc", "ClickMonitorDDC"),
        ("nvcplui", "NVIDIA Control Panel"),
        ("cnext", "AMD Radeon Software"),
    ];

    private readonly ILogger<WindowsColorConflictDetector> _logger;

    public WindowsColorConflictDetector(ILogger<WindowsColorConflictDetector> logger) => _logger = logger;

    public IReadOnlyList<string> FindRunningColorApplications()
    {
        HashSet<string> found = new(StringComparer.Ordinal);

        try
        {
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    foreach ((string name, string displayName) in KnownApplications)
                    {
                        if (string.Equals(process.ProcessName, name, StringComparison.OrdinalIgnoreCase))
                        {
                            found.Add(displayName);
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Enumerating processes can race with one exiting; the answer is
            // advisory either way.
            _logger.LogDebug(ex, "Could not enumerate processes while looking for colour applications.");
        }

        return [.. found];
    }
}
