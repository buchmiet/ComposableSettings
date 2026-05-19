namespace ComposableSettings;

public interface ISettingsComponentNameResolver
{
    string GetComponentName(Type componentType);
}
