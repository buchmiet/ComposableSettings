namespace ComposableSettings.Attributes;

/// <summary>
/// Marks a partial ViewModel that edits a <see cref="Document.SettingsEditingSession{T}"/>
/// draft with preview/commit semantics (Document profile).
/// The <c>SettingsDraftVmGenerator</c> emits draft accessors, preview wiring, and
/// <see cref="SettingsProxyAttribute"/> implementations — symmetric to
/// <see cref="SettingsVmAttribute"/> for the Composable (live-edit) profile.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public  class SettingsDraftVmAttribute(Type documentType) : Attribute
{
    public Type DocumentType { get; } = documentType;
}
