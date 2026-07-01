namespace ComposableSettings.Attributes;

/// <summary>
/// Marks a settings model (leaf). The <c>SettingsModelGenerator</c> turns the
/// class into an observable model: it implements <see cref="System.ComponentModel.INotifyPropertyChanged"/>
/// and emits a public, change-raising property for every instance field named
/// <c>_camelCase</c> (defaults come from the field initializers).
///
/// The class must be <c>partial</c> and must NOT already implement INPC.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public  class SettingsModelAttribute : Attribute;
