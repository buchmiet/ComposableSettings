namespace ComposableSettings;

public interface IComponentSettingsInitializer
{
    void Initialize(bool resetToDefaults = false);
}
