using ComposableSettings.Runtime;

namespace ComposableSettings.Abstractions;

public interface IComponentSettingsStore
{
    TSettings Get<TSettings>(SettingsNodePath path)
        where TSettings : class, new();

    void Set<TSettings>(SettingsNodePath path, TSettings value)
        where TSettings : class, new();

    void Register<TSettings>(SettingsNodePath path)
        where TSettings : class, new();

    void CompleteRegistration(bool resetToDefaults = false);
}