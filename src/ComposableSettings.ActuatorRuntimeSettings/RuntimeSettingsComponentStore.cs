using RuntimeSettings;

namespace ComposableSettings.ActuatorRuntimeSettings;

public sealed class RuntimeSettingsComponentStore : IComponentSettingsStore
{
    private readonly IRuntimeSettings _runtimeSettings;

    public RuntimeSettingsComponentStore(IRuntimeSettings runtimeSettings)
    {
        _runtimeSettings = runtimeSettings ?? throw new ArgumentNullException(nameof(runtimeSettings));
    }

    public TSettings Get<TSettings>(SettingsNodePath path)
        where TSettings : class, new()
    {
        return _runtimeSettings.Get<TSettings>(ToSettingsPath(path));
    }

    public void Set<TSettings>(SettingsNodePath path, TSettings value)
        where TSettings : class, new()
    {
        _runtimeSettings.Set(ToSettingsPath(path), value);
    }

    public void Register<TSettings>(SettingsNodePath path)
        where TSettings : class, new()
    {
        _runtimeSettings.Register(new SettingsRegistration<TSettings>
        {
            Path = ToSettingsPath(path),
            CreateDefaults = () => new TSettings(),
            Apply = _ => { }
        });
    }

    public void CompleteRegistration(bool resetToDefaults = false)
    {
        _runtimeSettings.CompleteRegistration(resetToDefaults);
    }

    private static SettingsPath ToSettingsPath(SettingsNodePath path)
    {
        return new SettingsPath(path.Segments.ToArray());
    }
}
