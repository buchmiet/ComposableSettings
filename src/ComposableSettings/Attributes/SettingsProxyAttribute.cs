namespace ComposableSettings;

/// <summary>
/// Opt-in "flat" projection of a single settings property onto the consuming VM.
/// Declared on a <c>partial</c> property whose name matches a property on the settings
/// model; the <c>ObservableSettingsGenerator</c> emits the implementing part that
/// forwards <c>get</c>/<c>set</c> to <c>Settings.X</c> (no backing field on the VM).
///
/// Change notification is handled by the generated relay: when the model raises
/// <c>PropertyChanged("X")</c>, the VM raises its own <c>OnPropertyChanged("X")</c>.
///
/// Symmetric in spirit with CommunityToolkit's <c>[ObservableProperty]</c>, but the
/// state lives in the shared settings model, not in the VM.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingsProxyAttribute : Attribute;
