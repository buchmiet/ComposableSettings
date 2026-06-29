using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Document;

public static class DocumentServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISettingsDocumentStore{TDocument}"/> (Document profile).
    /// Does not register Composable live-edit <see cref="Observable.ISettingsProvider{TSettings}"/>.
    /// </summary>
    public static IServiceCollection AddComposableSettingsDocument<TDocument>(
        this IServiceCollection services,
        Action<SettingsDocumentOptionsBuilder<TDocument>> configure)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var registration = DocumentRegistrationHost.GetOrAdd<TDocument>(services);
        registration.AddDocument(configure);
        return services;
    }
}
