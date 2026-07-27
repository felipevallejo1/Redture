using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Gamma;
using Redture.Platform.Linux.Displays;
using Redture.Platform.Linux.Gamma;

namespace Redture.Platform.Linux;

/// <summary>
/// Registers the Linux implementations of the platform contracts.
/// </summary>
/// <remarks>
/// <para>
/// Only what X11 can actually deliver is registered. Everything else falls
/// through to the null backends the host already registers, so the application
/// runs with a smaller feature set rather than not running.
/// </para>
/// <para>
/// Wayland is deliberately absent. There is no standard protocol for setting a
/// colour lookup table: <c>wlr-gamma-control-unstable-v1</c> exists but is a
/// wlroots extension, so it covers Sway and Hyprland and not GNOME or KDE, and
/// it needs the Wayland wire protocol rather than a handful of P/Invokes. A
/// session with no X server gets an honest "not supported here" instead.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
public static class LinuxPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddLinuxPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Decided once, at startup: either there is a server or there is not,
        // and everything downstream is chosen from that single answer.
        services.TryAddSingleton(provider =>
            X11Connection.Open(provider.GetRequiredService<ILogger<X11Connection>>()));

        services.AddSingleton<IDisplayEnumerator>(provider =>
        {
            X11Connection connection = provider.GetRequiredService<X11Connection>();

            return connection.IsConnected
                ? new X11DisplayEnumerator(connection, provider.GetRequiredService<ILogger<X11DisplayEnumerator>>())
                : new UnsupportedDisplayEnumerator(provider.GetRequiredService<ILogger<UnsupportedDisplayEnumerator>>());
        });

        services.AddSingleton<IGammaController>(provider =>
        {
            X11Connection connection = provider.GetRequiredService<X11Connection>();

            return connection.IsConnected
                ? new X11GammaController(connection, provider.GetRequiredService<ILogger<X11GammaController>>())
                : new NullGammaController(provider.GetRequiredService<ILogger<NullGammaController>>());
        });

        return services;
    }
}
