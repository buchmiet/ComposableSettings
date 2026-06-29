using ComposableSettings.Document;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Packs;

public static class PacksServiceCollectionExtensions
{
    /// <summary>Registers pack loader, catalog, and wires pack overlay into the document store.</summary>
    public static IServiceCollection AddComposableSettingsPacks<TDocument>(
        this IServiceCollection services,
        Action<SettingsPackOptions> configure,
        Func<TDocument, string?>? packIdResolver = null)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var registration = DocumentRegistrationHost.GetOrAdd<TDocument>(services);
        registration.ConfigurePacks(configure, packIdResolver);

        if (!services.Any(d => d.ServiceType == typeof(SettingsPackOptions)))
        {
            services.AddSingleton(_ =>
            {
                var options = new SettingsPackOptions();
                configure(options);
                return options;
            });
        }

        if (!services.Any(d => d.ServiceType == typeof(ISettingsPackLoader)))
            services.AddSingleton<ISettingsPackLoader>(sp => new SettingsPackLoader(sp.GetRequiredService<SettingsPackOptions>()));

        if (!services.Any(d => d.ServiceType == typeof(ISettingsPackCatalog<TDocument>)))
        {
            services.AddSingleton<ISettingsPackCatalog<TDocument>>(sp =>
            {
                var catalog = sp.GetRequiredService<SettingsDocumentRegistration<TDocument>>().Catalog
                              ?? throw new InvalidOperationException(
                                  $"Call AddComposableSettingsPacks<{typeof(TDocument).Name}>() to register the catalog.");
                return catalog;
            });
        }

        return services;
    }

    public static IServiceCollection AddComposableSettingsPackExporter<TDocument>(
        this IServiceCollection services)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!services.Any(d => d.ServiceType == typeof(ISettingsPackExporter<TDocument>)))
        {
            services.AddSingleton<ISettingsPackExporter<TDocument>>(sp =>
            {
                var registration = sp.GetRequiredService<SettingsDocumentRegistration<TDocument>>();
                return new SettingsPackExporter<TDocument>(registration.Serializer);
            });
        }

        return services;
    }
}
