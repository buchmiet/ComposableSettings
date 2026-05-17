using System.Collections.Concurrent;

namespace ComposableSettings;

public sealed class InMemoryComponentSettingsStore : IComponentSettingsStore
{
    private readonly ConcurrentDictionary<SettingsStoreKey, object> _settings = new();

    public TSettings Get<TSettings>(SettingsNodePath path)
        where TSettings : class, new()
    {
        ArgumentNullException.ThrowIfNull(path);
        var key = new SettingsStoreKey(path.ToString(), typeof(TSettings));
        return (TSettings)_settings.GetOrAdd(key, _ => new TSettings());
    }

    public void Set<TSettings>(SettingsNodePath path, TSettings value)
        where TSettings : class, new()
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(value);
        var key = new SettingsStoreKey(path.ToString(), typeof(TSettings));
        _settings[key] = value;
    }

    public void Register<TSettings>(SettingsNodePath path)
        where TSettings : class, new()
    {
    }

    public void CompleteRegistration(bool resetToDefaults = false)
    {
    }

    private readonly record struct SettingsStoreKey(string Path, Type SettingsType);
}
