using System.ComponentModel;
using ComposableSettings.Runtime;
using ComposableSettings.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings;

/// <summary>
/// DI for the new contract. Each OWNER registers its OWN file (keyed), then its
/// providers point at that file + a node. The runtime host and the GUI host call
/// these independently — no shared file, no cross-knowledge.
///
/// (A future source generator can emit <see cref="AddSettingsProvider{TSettings}"/>
/// calls per leaf so this composition is generated, not hand-written.)
/// </summary>
public static class ObservableSettingsServiceCollectionExtensions
{
    /// <summary>Registers a settings file under <paramref name="fileKey"/> (XML, per-owner).</summary>
    public static IServiceCollection AddComposableSettingsFile(
        this IServiceCollection services, string fileKey, string filePath)
    {
        services.AddKeyedSingleton<IComponentSettingsProvider>(
            fileKey, (_, _) => new XmlSettingsFile(filePath));
        return services;
    }

    /// <summary>Registers an already-built file under <paramref name="fileKey"/> (e.g. in-memory for tests).</summary>
    public static IServiceCollection AddComposableSettingsFile(
        this IServiceCollection services, string fileKey, IComponentSettingsProvider file)
    {
        services.AddKeyedSingleton<IComponentSettingsProvider>(fileKey, (_, _) => file);
        return services;
    }

    /// <summary>Registers <see cref="ISettingsProvider{TSettings}"/> bound to a file + node.</summary>
    public static IServiceCollection AddSettingsProvider<TSettings>(
        this IServiceCollection services, string fileKey, SettingsNodePath node)
        where TSettings : class, INotifyPropertyChanged, new()
        => services.AddSettingsProvider<TSettings>(fileKey, node, SettingsProviderOptions.Default);

    /// <summary>
    /// Registers <see cref="ISettingsProvider{TSettings}"/> bound to a file + node
    /// and coalesces writes to the backing store with <paramref name="persistDebounceDelay"/>.
    /// </summary>
    public static IServiceCollection AddSettingsProvider<TSettings>(
        this IServiceCollection services,
        string fileKey,
        SettingsNodePath node,
        TimeSpan persistDebounceDelay)
        where TSettings : class, INotifyPropertyChanged, new()
        => services.AddSettingsProvider<TSettings>(
            fileKey,
            node,
            new SettingsProviderOptions { PersistDebounceDelay = persistDebounceDelay });

    /// <summary>Registers <see cref="ISettingsProvider{TSettings}"/> bound to a file + node.</summary>
    public static IServiceCollection AddSettingsProvider<TSettings>(
        this IServiceCollection services,
        string fileKey,
        SettingsNodePath node,
        SettingsProviderOptions? options)
        where TSettings : class, INotifyPropertyChanged, new()
    {
        services.AddSingleton<ISettingsProvider<TSettings>>(sp =>
            new SettingsProvider<TSettings>(
                sp.GetRequiredKeyedService<IComponentSettingsProvider>(fileKey),
                node,
                options ?? SettingsProviderOptions.Default));
        return services;
    }
}
