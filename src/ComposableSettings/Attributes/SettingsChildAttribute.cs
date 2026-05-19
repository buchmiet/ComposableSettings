namespace ComposableSettings.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public  class SettingsChildAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}