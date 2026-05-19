namespace ComposableSettings.Abstractions;

public interface ISettingsComponentNameResolver
{
    string GetComponentName(Type componentType);
}