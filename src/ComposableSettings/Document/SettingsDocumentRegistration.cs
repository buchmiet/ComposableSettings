using ComposableSettings.Layering;
using ComposableSettings.Packs;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Document;

internal  class SettingsDocumentRegistration<TDocument>
    where TDocument : class, new()
{
    private readonly List<Action<SettingsDocumentOptionsBuilder<TDocument>>> _documentConfigs = [];
    private readonly List<Action<SettingsMergePolicy>> _layeringConfigs = [];
    private SettingsPackOptions? _packOptions;
    private Func<TDocument, string?>? _packIdResolver;
    private ISettingsDocumentStore<TDocument>? _built;
    private ISettingsPackCatalog<TDocument>? _catalog;
    private ISettingsDocumentSerializer<TDocument>? _serializer;

    internal ISettingsDocumentSerializer<TDocument> Serializer =>
        _serializer ?? new JsonSettingsDocumentSerializer<TDocument>();

    internal ISettingsPackCatalog<TDocument>? Catalog
    {
        get
        {
            Build();
            return _catalog;
        }
    }

    public void AddDocument(Action<SettingsDocumentOptionsBuilder<TDocument>> configure) =>
        _documentConfigs.Add(configure);

    public void AddLayering(Action<SettingsMergePolicy> configure) =>
        _layeringConfigs.Add(configure);

    public void ConfigurePacks(Action<SettingsPackOptions> configure, Func<TDocument, string?>? packIdResolver)
    {
        _packOptions ??= new SettingsPackOptions();
        configure(_packOptions);
        if (packIdResolver is not null)
            _packIdResolver = packIdResolver;
    }

    public ISettingsDocumentStore<TDocument> Build()
    {
        if (_built is not null)
            return _built;

        if (_documentConfigs.Count == 0)
            throw new InvalidOperationException(
                $"Call AddComposableSettingsDocument<{typeof(TDocument).Name}>() before resolving the store.");

        var builder = new SettingsDocumentOptionsBuilder<TDocument>();
        foreach (var configure in _documentConfigs)
            configure(builder);

        var options = builder.Build();
        _serializer = options.Serializer ?? new JsonSettingsDocumentSerializer<TDocument>();

        ISettingsLayerMerger<TDocument>? merger = null;
        if (_layeringConfigs.Count > 0)
        {
            var policy = new SettingsMergePolicy();
            foreach (var configure in _layeringConfigs)
                configure(policy);

            merger = new JsonSettingsLayerMerger<TDocument>(policy, _serializer);
        }

        if (_packOptions is not null)
        {
            var loader = new SettingsPackLoader(_packOptions);
            _catalog = new SettingsPackCatalog<TDocument>(_packOptions, loader, _serializer);
        }

        _built = new SettingsDocumentStore<TDocument>(options, merger, _catalog, _packIdResolver);
        if (_catalog is not null && _built is SettingsDocumentStore<TDocument> concreteStore)
            _catalog.PackCacheChanged += (_, _) => concreteStore.ReloadFromPackCache();

        return _built;
    }
}

internal static class DocumentRegistrationHost
{
    public static SettingsDocumentRegistration<TDocument> GetOrAdd<TDocument>(IServiceCollection services)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(SettingsDocumentRegistration<TDocument>)
                && descriptor.ImplementationInstance is SettingsDocumentRegistration<TDocument> existing)
            {
                return existing;
            }
        }

        var registration = new SettingsDocumentRegistration<TDocument>();
        services.AddSingleton(registration);
        services.AddSingleton<ISettingsDocumentStore<TDocument>>(sp =>
            sp.GetRequiredService<SettingsDocumentRegistration<TDocument>>().Build());
        return registration;
    }
}
