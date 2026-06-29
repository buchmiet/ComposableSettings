using Microsoft.CodeAnalysis;

namespace ComposableSettings.Generator;

internal static class GeneratorDiagnostics
{
    public static readonly DiagnosticDescriptor ConflictsWithSettingsDraftVm = new(
        "CSP041",
        "SettingsVm and SettingsDraftVm cannot be combined",
        "Class '{0}' cannot use both [SettingsVm] and [SettingsDraftVm]",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProxyMustBePartial = new(
        "CSP047",
        "Settings proxy property must be partial",
        "[SettingsProxy] property '{0}' on '{1}' must be declared 'partial' so the generator can emit the accessor body",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
