namespace ComposableSettings;

[AttributeUsage(AttributeTargets.Class)]
public sealed class SettingsRootAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
