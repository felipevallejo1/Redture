using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Gamma;
using Redture.Platform.MacOS.Displays;
using Redture.Platform.MacOS.Gamma;

namespace Redture.Platform.MacOS;

/// <summary>
/// Registers the macOS implementations of the platform contracts.
/// </summary>
/// <remarks>
/// <b>Unverified.</b> Written from the published Core Graphics API and compiled
/// on every CI leg, but never executed on a Mac. It is registered anyway
/// because every failure path degrades to "unsupported" rather than throwing:
/// the worst case on a real Mac is that colour temperature does nothing and
/// <c>tools/Redture.Diagnostics</c> reports it, which is a better starting
/// point for whoever has the hardware than an empty folder.
/// </remarks>
[SupportedOSPlatform("macos")]
public static class MacOsPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddMacOsPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDisplayEnumerator, MacDisplayEnumerator>();
        services.TryAddSingleton<IGammaController, MacGammaController>();

        return services;
    }
}
