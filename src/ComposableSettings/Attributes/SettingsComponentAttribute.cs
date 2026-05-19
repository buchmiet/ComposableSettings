namespace ComposableSettings.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public  class SettingsComponentAttribute : Attribute
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

    /// <summary>
    ///     Whether to generate the async-first lifecycle (Settings property,
    ///     SaveSettingsAsync, ResetSettingsAsync, SettingsUpdatedAsync, and the
    ///     component constructor). Opt-out: defaults to <c>true</c> whenever a
    ///     settings type is declared. Set to <c>false</c> to make the component a
    ///     pure grouping/tree node without lifecycle.
    /// </summary>
    public bool GenerateLifecycle { get; set; } = true;
}