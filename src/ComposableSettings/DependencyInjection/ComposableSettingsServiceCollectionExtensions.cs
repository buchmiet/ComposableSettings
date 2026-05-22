using ComposableSettings.Abstractions;
using ComposableSettings.Configuration;
using ComposableSettings.Runtime;
using ComposableSettings.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.DependencyInjection;

public static class ComposableSettingsServiceCollectionExtensions
{
    public static IServiceCollection AddComposableSettingsProviders(
        this IServiceCollection services, params SettingsOptions[] componentSettingsStore)
    {
        services.AddSingleton<SettingsNodeContextAccessor>();
        services.AddTransient<ISettingsNodeContext>(provider =>
            provider.GetRequiredService<SettingsNodeContextAccessor>().Current);
        services.AddSingleton<ISettingsComponentNameResolver, SettingsComponentNameResolver>();
        services.AddSingleton<ISettingsNodeFactory, SettingsNodeFactory>();
        services.AddTransient(typeof(IComponentSettings<>), typeof(ComponentSettings<>));
        foreach (var settingsOptions in componentSettingsStore)
        {
            switch (settingsOptions.PersistenceType)
            {
                case PersistenceType.XmlFile:
                    services.AddSingleton<ISettingsStore, XmlSettingsStore>();
                    break;
            }
        }
        services.AddSingleton(componentSettingsStore);
        return services;
    }


}