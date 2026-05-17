namespace ComposableSettings;

public interface ISettingsNodeContext
{
    SettingsNodePath Path { get; }
    string InstanceName { get; }
    string ComponentName { get; }
    Type ComponentType { get; }
}
