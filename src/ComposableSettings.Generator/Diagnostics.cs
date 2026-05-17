using Microsoft.CodeAnalysis;

namespace ComposableSettings.Generator;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor ParentClassMustBePartial = new(
        "CSP001",
        "Settings parent class must be partial",
        "Class '{0}' contains settings children and must be partial.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ChildPropertyMustBeAssignable = new(
        "CSP002",
        "Settings child property must be assignable",
        "Settings child property '{0}' must be assignable by generated code.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateChildNodeName = new(
        "CSP003",
        "Duplicate settings child node name",
        "Duplicate settings child node '{0}' in '{1}'. Use explicit SettingsChild names.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidChildName = new(
        "CSP004",
        "Invalid settings child name",
        "Settings child name '{0}' is not a valid path segment.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidComponentName = new(
        "CSP005",
        "Invalid settings component name",
        "Settings component name '{0}' is not a valid path segment.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NestedParentClassNotSupported = new(
        "CSP006",
        "Nested settings parent class is not supported",
        "Nested settings parent class '{0}' is not supported by this generator.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GeneratedInitializationMethodAlreadyExists = new(
        "CSP007",
        "Generated settings initialization method already exists",
        "Class '{0}' already contains a method named 'InitializeGeneratedSettingsChildren' with the generated signature.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidRootName = new(
        "CSP008",
        "Invalid settings root name",
        "Settings root name '{0}' is not a valid path segment.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateSettingsRegistrationPath = new(
        "CSP009",
        "Duplicate settings registration path",
        "Duplicate settings registration path '{0}' for type '{1}'. Ensure each settings path is unique.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ComponentNotReachableFromRoot = new(
        "CSP010",
        "Settings component not reachable from any root",
        "Settings component '{0}' with settings type '{1}' is not reachable from any [SettingsRoot] tree.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleClassMustBePartial = new(
        "CSP011",
        "Lifecycle class must be partial",
        "Class '{0}' has GenerateLifecycle=true but is not partial.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleSettingsTypeMissing = new(
        "CSP012",
        "Lifecycle requires settings type",
        "Class '{0}' has GenerateLifecycle=true but no SettingsType is declared on [SettingsComponent].",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleSettingsTypeNoParameterlessCtor = new(
        "CSP013",
        "Lifecycle settings type must have a public parameterless constructor",
        "Settings type '{0}' used by component '{1}' has GenerateLifecycle=true but lacks a public parameterless constructor.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleMemberConflict = new(
        "CSP014",
        "Lifecycle member already exists",
        "Component '{0}' with GenerateLifecycle=true already defines member '{1}'. Remove the member or disable lifecycle generation.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleUserDefinedConstructorNotSupported = new(
        "CSP015",
        "Lifecycle generation does not support user-defined constructors yet",
        "Class '{0}' has GenerateLifecycle=true and declares a constructor. Remove the constructor, disable lifecycle generation, or wait for constructor integration support. Source generation was skipped.",
        "ComposableSettings.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
