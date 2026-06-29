using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Document;

public static class DocumentServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISettingsDocumentStore{TDocument}"/> (Document profile).
    /// Does not register Composable live-edit <see cref="ISettingsProvider{TSettings}"/>.
    /// </summary>
    public static IServiceCollection AddComposableSettingsDocument<TDocument>(
        this IServiceCollection services,
        Action<SettingsDocumentOptionsBuilder<TDocument>> configure)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new SettingsDocumentOptionsBuilder<TDocument>();
        configure(builder);
        var options = builder.Build();

        services.AddSingleton<ISettingsDocumentStore<TDocument>>(_ => new SettingsDocumentStore<TDocument>(options));
        return services;
    }
}
