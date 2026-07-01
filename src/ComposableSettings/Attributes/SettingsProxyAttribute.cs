namespace ComposableSettings.Attributes;

/// <summary>
/// Opt-in projection of a settings/document property onto the consuming VM.
/// Declared on a <c>partial</c> property; the generator emits <c>get</c>/<c>set</c>
/// forwarding to the live model (<c>[SettingsVm]</c>) or draft
/// (<c>[SettingsDraftVm]</c>) — no backing field on the VM.
///
/// For <c>[SettingsVm]</c>: default path is the property name at document root.
/// For <c>[SettingsDraftVm]</c>: use <see cref="MemberPath"/> or
/// <see cref="SettingsDraftRootAttribute"/> for nested sections.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public  class SettingsProxyAttribute : Attribute
{
    /// <summary>
    /// Dot-path from document root, or relative to <see cref="SettingsDraftRootAttribute"/>.
    /// When null, the generator uses the declaring property name.
    /// </summary>
    public string? MemberPath { get; }

    public SettingsProxyAttribute()
    {
    }

    public SettingsProxyAttribute(string memberPath) => MemberPath = memberPath;
}
