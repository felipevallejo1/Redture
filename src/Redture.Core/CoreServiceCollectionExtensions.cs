using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Redture.Core.Infrastructure;
using Redture.Core.Settings;

namespace Redture.Core;

/// <summary>
/// Registers the platform-agnostic services. The host application composes
/// this with exactly one platform module (see Redture.Platform.*).
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddRedtureCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd, so a host that already resolved its own paths (the app does,
        // because logging must be configured before the container exists) keeps
        // that instance instead of getting a second one.
        services.TryAddSingleton<IAppPaths>(_ => AppPaths.CreateDefault());
        services.TryAddSingleton<ISettingsStore, JsonSettingsStore>();
        services.TryAddSingleton<CleanShutdownSentinel>();

        return services;
    }
}
