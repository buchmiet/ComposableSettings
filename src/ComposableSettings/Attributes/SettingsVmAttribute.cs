using ComposableSettings.Observable;

namespace ComposableSettings.Attributes;

/// <summary>
/// Marks a ViewModel/component that consumes a settings model but ALREADY implements
/// <see cref="System.ComponentModel.INotifyPropertyChanged"/> (e.g. a CommunityToolkit
/// <c>ObservableObject</c>). Unlike <see cref="SettingsConsumerAttribute"/> — which owns
/// INPC and therefore trips CSP024 on an ObservableObject — the
/// <c>ObservableSettingsGenerator</c> RELAYS into the existing INPC by calling the
/// class's <c>OnPropertyChanged(string)</c>.
///
/// The generator emits: a provider field, a pass-through <c>Settings</c> property
/// (the component stores no settings state), an <c>InitializeSettings(provider)</c>
/// method the user calls from their own constructor, the change relay, and the
/// implementing parts of any <c>[SettingsProxy]</c> partial properties.
///
/// Named <c>SettingsVm</c> (not <c>ObservableSettings</c>) to avoid colliding with the
/// <see cref="ObservableSettings"/> base class during attribute name resolution
/// (C# probes both <c>Foo</c> and <c>FooAttribute</c>).
///
/// The class must be <c>partial</c> and must expose an accessible
/// <c>OnPropertyChanged(string)</c> (satisfied by deriving from ObservableObject).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public  class SettingsVmAttribute(Type settingsType) : Attribute
{
    public Type SettingsType { get; } = settingsType;
}
