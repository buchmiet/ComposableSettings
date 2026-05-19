using ComposableSettings.Abstractions;
using ComposableSettings.Runtime;
using ComposableSettings.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.DependencyInjection;

public static class ComposableSettingsServiceCollectionExtensions
{
    public static IServiceCollection AddComposableSettingsInfrastructure(
        this IServiceCollection services)
    {
        services.AddSingleton<SettingsNodeContextAccessor>();
        services.AddTransient<ISettingsNodeContext>(provider =>
            provider.GetRequiredService<SettingsNodeContextAccessor>().Current);

        services.AddSingleton<ISettingsComponentNameResolver, SettingsComponentNameResolver>();
        services.AddSingleton<ISettingsNodeFactory, SettingsNodeFactory>();
        services.AddTransient(typeof(IComponentSettings<>), typeof(ComponentSettings<>));

        return services;
    }

    public static IServiceCollection AddInMemoryComposableSettingsStore(
        this IServiceCollection services)
    {
        services.AddSingleton<IComponentSettingsStore, InMemoryComponentSettingsStore>();
        return services;
    }
}