namespace ComposableSettings;

public sealed class SettingsNodeContext : ISettingsNodeContext
{
    public SettingsNodeContext(
        SettingsNodePath path,
        string instanceName,
        string componentName,
        Type componentType)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        InstanceName = SettingsNodePath.ValidateSegment(instanceName);
        ComponentName = SettingsNodePath.ValidateSegment(componentName);
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
    }

    public SettingsNodePath Path { get; }

    public string InstanceName { get; }

    public string ComponentName { get; }

    public Type ComponentType { get; }
}
