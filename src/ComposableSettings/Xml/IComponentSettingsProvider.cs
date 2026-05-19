using ComposableSettings.Runtime;

namespace ComposableSettings.Xml;

public interface IComponentSettingsProvider
{
    string SettingsFilePath { get; }

    TSettings Get<TSettings>(SettingsNodePath path)
        where TSettings : class, new();

    void Set<TSettings>(SettingsNodePath path, TSettings value)
        where TSettings : class, new();
}