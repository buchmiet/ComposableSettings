namespace ComposableSettings;

public interface ISettingsNodeFactory
{
    T CreateChild<T>(
        SettingsNodePath parentPath,
        string? instanceName = null)
        where T : class;

    object CreateChild(
        Type childType,
        SettingsNodePath parentPath,
        string? instanceName = null);
}
