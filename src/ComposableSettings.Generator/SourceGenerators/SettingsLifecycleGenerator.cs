using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using ComposableSettings.Generator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ComposableSettings.Generator.SourceGenerators;

[Generator]
public  class SettingsLifecycleGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var componentClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, ct) => GetComponentInfo(ctx))
            .Where(static c => c is not null);

        context.RegisterSourceOutput(componentClasses.Collect(), GenerateLifecycle);
    }

    private static ComponentInfo? GetComponentInfo(GeneratorSyntaxContext context)
    {
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
            return null;

        var attribute = classSymbol.GetAttribute(GeneratorConstants.SettingsComponentAttributeFullName);
        if (attribute is null) return null;

        var name = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : SettingsNameHelper.ResolveComponentName(classSymbol);

        INamedTypeSymbol? settingsType = null;
        if (attribute.ConstructorArguments.Length > 1
            && attribute.ConstructorArguments[1].Value is INamedTypeSymbol st)
            settingsType = st;

        // GenerateLifecycle is opt-out (lifecycle/async-first by default):
        //  - explicit GenerateLifecycle = false  -> skip (no lifecycle)
        //  - explicit GenerateLifecycle = true    -> generate (CSP012 if no settings type)
        //  - omitted + a settings type declared   -> generate (the default)
        //  - omitted + no settings type           -> pure grouping node, skip silently
        bool? explicitFlag = null;
        foreach (var namedArg in attribute.NamedArguments)
            if (namedArg.Key == "GenerateLifecycle")
            {
                explicitFlag = namedArg.Value.Value is true;
                break;
            }

        var generateLifecycle = explicitFlag ?? settingsType is not null;
        if (!generateLifecycle) return null;

        return new ComponentInfo(
            classSyntax,
            classSymbol,
            name ?? string.Empty,
            settingsType);
    }

    private static void GenerateLifecycle(
        SourceProductionContext context,
        ImmutableArray<ComponentInfo?> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is null) continue;
            GenerateLifecycleForComponent(context, candidate);
        }
    }

    private static void GenerateLifecycleForComponent(
        SourceProductionContext context,
        ComponentInfo info)
    {
        var hasError = false;

        // CSP012: no settings type
        if (info.SettingsType is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.LifecycleSettingsTypeMissing,
                info.Syntax.Identifier.GetLocation(),
                info.ClassName));
            return;
        }

        // CSP011: not partial
        if (!info.Symbol.IsPartial())
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.LifecycleClassMustBePartial,
                info.Syntax.Identifier.GetLocation(),
                info.ClassName));
            hasError = true;
        }

        // CSP013: settings type must have public parameterless ctor
        var hasParameterlessCtor = HasPublicParameterlessConstructor(info.SettingsType);
        if (!hasParameterlessCtor)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.LifecycleSettingsTypeNoParameterlessCtor,
                info.Syntax.Identifier.GetLocation(),
                info.SettingsType.Name,
                info.ClassName));
            hasError = true;
        }

        // CSP014: member conflicts
        var memberConflicts = CheckMemberConflicts(info.Symbol);
        foreach (var conflict in memberConflicts)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.LifecycleMemberConflict,
                info.Syntax.Identifier.GetLocation(),
                info.ClassName,
                conflict));
            hasError = true;
        }

        // CSP015: user-defined constructors not supported
        if (HasUserDefinedConstructor(info.Symbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.LifecycleUserDefinedConstructorNotSupported,
                info.Syntax.Identifier.GetLocation(),
                info.ClassName));
            hasError = true;
        }

        if (hasError) return;

        var settingsTypeGlobalName = info.SettingsType.ToGlobalTypeName();

        var source = BuildLifecycleSource(
            info.Symbol,
            info.Symbol.Name,
            settingsTypeGlobalName);

        context.AddSource(
            $"{info.Symbol.ToDisplayString().Replace('.', '_')}.Lifecycle.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
    {
        if (type.IsValueType) return true;

        var ctors = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Constructor && m.DeclaredAccessibility == Accessibility.Public);

        foreach (var ctor in ctors)
            if (ctor.Parameters.Length == 0)
                return true;

        // If no constructors are defined, compiler provides a public default
        var hasExplicitConstructors = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m => m.MethodKind == MethodKind.Constructor && !m.IsStatic);

        return !hasExplicitConstructors;
    }

    private static bool HasUserDefinedConstructor(INamedTypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m => m.MethodKind == MethodKind.Constructor && !m.IsStatic && !m.IsImplicitlyDeclared);
    }

    private static string[] CheckMemberConflicts(INamedTypeSymbol type)
    {
        var conflicts = new List<string>();
        var memberNames = new HashSet<string>(type.GetMembers().Select(m => m.Name));

        if (memberNames.Contains("Settings")) conflicts.Add("Settings");
        if (memberNames.Contains("ResetSettingsAsync")) conflicts.Add("ResetSettingsAsync");
        if (memberNames.Contains("SaveSettingsAsync")) conflicts.Add("SaveSettingsAsync");
        if (memberNames.Contains("SettingsUpdatedAsync")) conflicts.Add("SettingsUpdatedAsync");
        if (memberNames.Contains("_componentSettings")) conflicts.Add("_componentSettings");

        return conflicts.ToArray();
    }

    private static string BuildLifecycleSource(
        INamedTypeSymbol type,
        string className,
        string settingsTypeGlobalName)
    {
        var accessibility = type.GetAccessibilityText();
        var namespaceName = type.GetNamespaceName();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine($"namespace {namespaceName};");
            sb.AppendLine();
        }

        sb.AppendLine($"{accessibility} partial class {className}");
        sb.AppendLine("{");
        sb.AppendLine(
            $"    private readonly global::ComposableSettings.IComponentSettings<{settingsTypeGlobalName}> _componentSettings;");
        sb.AppendLine();
        sb.AppendLine(
            $"    public {className}(global::ComposableSettings.IComponentSettings<{settingsTypeGlobalName}> componentSettings)");
        sb.AppendLine("    {");
        sb.AppendLine("        _componentSettings = componentSettings;");
        sb.AppendLine($"        Settings = new {settingsTypeGlobalName}();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine(
            $"    public {settingsTypeGlobalName} Settings {{ get; private set; }} = new {settingsTypeGlobalName}();");
        sb.AppendLine();
        sb.AppendLine("    public async Task ResetSettingsAsync(");
        sb.AppendLine("        CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine($"        Settings = new {settingsTypeGlobalName}();");
        sb.AppendLine();
        sb.AppendLine("        await SettingsUpdatedAsync(Settings, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public async Task SaveSettingsAsync(");
        sb.AppendLine("        CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        await _componentSettings.SaveAsync(Settings, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected virtual Task SettingsUpdatedAsync(");
        sb.AppendLine($"        {settingsTypeGlobalName} settings,");
        sb.AppendLine("        CancellationToken cancellationToken)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Task.CompletedTask;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private  class ComponentInfo(
        ClassDeclarationSyntax syntax,
        INamedTypeSymbol symbol,
        string name,
        INamedTypeSymbol? settingsType)
    {
        public ClassDeclarationSyntax Syntax { get; } = syntax;
        public INamedTypeSymbol Symbol { get; } = symbol;
        public string ClassName { get; } = name;
        public INamedTypeSymbol? SettingsType { get; } = settingsType;
    }
}