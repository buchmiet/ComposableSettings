using Microsoft.Extensions.DependencyInjection;
using RuntimeSettings;

namespace ComposableSettings.ActuatorRuntimeSettings;

public static class ActuatorRuntimeSettingsServiceCollectionExtensions
{
    public static IServiceCollection AddActuatorRuntimeSettingsStore(
        this IServiceCollection services,
        RuntimeSettingsOptions options)
    {
        services.AddSingleton<IRuntimeSettings>(_ => new XmlRuntimeSettings(options));
        services.AddSingleton<IComponentSettingsStore, RuntimeSettingsComponentStore>();
        return services;
    }
}
