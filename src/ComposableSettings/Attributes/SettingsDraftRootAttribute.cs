namespace ComposableSettings;

/// <summary>
/// Optional prefix for <see cref="SettingsProxyAttribute"/> paths on a
/// <see cref="SettingsDraftVmAttribute"/> class. Example: <c>"Layout.Terminal"</c> so
/// <c>[SettingsProxy("Padding.Horizontal")]</c> resolves to
/// <c>Draft.Layout.Terminal.Padding.Horizontal</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public  class SettingsDraftRootAttribute(string path) : Attribute
{
    public string Path { get; } = path;
}
