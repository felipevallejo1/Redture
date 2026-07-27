using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Overlay;
using Redture.Platform.Abstractions.SystemEvents;
using Redture.Platform.Windows.Displays;
using Redture.Platform.Windows.Overlay;
using Redture.Platform.Windows.SystemEvents;

namespace Redture.Platform.Windows;

/// <summary>
/// Registers the Windows implementations of the platform contracts.
/// </summary>
/// <remarks>
/// Marked <see cref="SupportedOSPlatformAttribute"/> so the platform-compatibility
/// analyzer forces every caller to guard the call with
/// <c>OperatingSystem.IsWindows()</c>.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class WindowsPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDisplayEnumerator, WindowsDisplayEnumerator>();
        services.AddSingleton<IOverlayController, WindowsOverlayController>();
        services.AddSingleton<ISystemEvents, WindowsSystemEvents>();

        return services;
    }
}
