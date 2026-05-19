namespace ComposableSettings.Abstractions;

public interface IComponentSettingsInitializer
{
    void Initialize(bool resetToDefaults = false);
}