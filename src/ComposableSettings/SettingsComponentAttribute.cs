namespace ComposableSettings;

[AttributeUsage(AttributeTargets.Class)]
public sealed class SettingsComponentAttribute : Attribute
{
    public SettingsComponentAttribute(string name)
    {
        Name = name;
    }

    public SettingsComponentAttribute(string name, Type settingsType)
    {
        Name = name;
        SettingsType = settingsType;
    }

    public string Name { get; }

    public Type? SettingsType { get; }

    public bool GenerateLifecycle { get; set; }
}
