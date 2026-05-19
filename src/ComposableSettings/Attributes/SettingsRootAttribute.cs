namespace ComposableSettings.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public  class SettingsRootAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}