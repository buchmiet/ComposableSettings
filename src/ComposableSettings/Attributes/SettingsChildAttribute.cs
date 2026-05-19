namespace ComposableSettings;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingsChildAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}
