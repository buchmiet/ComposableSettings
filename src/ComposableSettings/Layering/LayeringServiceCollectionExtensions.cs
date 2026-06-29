using ComposableSettings.Document;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Layering;

public static class LayeringServiceCollectionExtensions
{
    /// <summary>Registers layered merge for an existing document registration.</summary>
    public static IServiceCollection AddComposableSettingsLayering<TDocument>(
        this IServiceCollection services,
        Action<SettingsMergePolicy> configure)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var registration = DocumentRegistrationHost.GetOrAdd<TDocument>(services);
        registration.AddLayering(configure);
        return services;
    }
}
