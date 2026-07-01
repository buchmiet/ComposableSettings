namespace ComposableSettings.Attributes;

/// <summary>
/// Marks a consumer (ViewModel/component) of a settings model. The
/// <c>SettingsConsumerGenerator</c> emits the provider field, a pass-through
/// <c>Settings</c> property (the component stores no settings state) and a
/// generated <c>InitializeGeneratedSettings(provider)</c> method that wires
/// <see cref="System.ComponentModel.INotifyPropertyChanged"/> relay.
///
/// The user calls <c>InitializeGeneratedSettings(provider)</c> in their own
/// constructor. The class must be <c>partial</c> and must NOT already implement INPC.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public  class SettingsConsumerAttribute(Type settingsType) : Attribute
{
    public Type SettingsType { get; } = settingsType;
}
