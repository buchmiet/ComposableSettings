namespace ComposableSettings.Runtime;

public interface IComponentSettingsProvider
{
    
    TSettings Get<TSettings>(SettingsNodePath path)
        where TSettings : class, new();

    void Set<TSettings>(SettingsNodePath path, TSettings value)
        where TSettings : class, new();
}