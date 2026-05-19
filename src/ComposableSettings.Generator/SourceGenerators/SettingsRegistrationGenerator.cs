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
public  class SettingsRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rootClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, ct) => GetRootInfo(ctx))
            .Where(static r => r is not null);

        var componentClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, ct) => GetComponentInfo(ctx))
            .Where(static c => c is not null);

        var childProperties = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is PropertyDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, ct) => GetChildInfo(ctx))
            .Where(static c => c is not null);

        context.RegisterSourceOutput(
            rootClasses.Collect().Combine(componentClasses.Collect()).Combine(childProperties.Collect()),
            GenerateRegistration);
    }

    private static RootInfo? GetRootInfo(GeneratorSyntaxContext context)
    {
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
            return null;

        var attribute = classSymbol.GetAttribute(GeneratorConstants.SettingsRootAttributeFullName);
        if (attribute?.ConstructorArguments.Length > 0
            && attribute.ConstructorArguments[0].Value is string rootName)
            return new RootInfo(classSymbol.ToGlobalTypeName(), rootName);

        return null;
    }

    private static ComponentInfo? GetComponentInfo(GeneratorSyntaxContext context)
    {
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
            return null;

        var attribute = classSymbol.GetAttribute(GeneratorConstants.SettingsComponentAttributeFullName);
        if (attribute is null)
            return null;

        var name = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : SettingsNameHelper.ResolveComponentName(classSymbol);

        string? settingsTypeName = null;
        if (attribute.ConstructorArguments.Length > 1
            && attribute.ConstructorArguments[1].Value is INamedTypeSymbol settingsType)
            settingsTypeName = settingsType.ToGlobalTypeName();

        return new ComponentInfo(classSymbol.ToGlobalTypeName(), name ?? string.Empty, settingsTypeName);
    }

    private static ChildInfo? GetChildInfo(GeneratorSyntaxContext context)
    {
        var propertySyntax = (PropertyDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(propertySyntax) is not IPropertySymbol propertySymbol)
            return null;

        if (!propertySymbol.HasAttribute(GeneratorConstants.SettingsChildAttributeFullName))
            return null;

        var parentType = propertySymbol.ContainingType;
        if (parentType is null) return null;

        var childType = propertySymbol.Type as INamedTypeSymbol;
        if (childType is null) return null;

        var attribute = propertySymbol.GetAttribute(GeneratorConstants.SettingsChildAttributeFullName);
        var explicitName = attribute?.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;

        var nodeName = explicitName ?? SettingsNameHelper.ResolveComponentName(childType);

        return new ChildInfo(
            parentType.ToGlobalTypeName(),
            childType.ToGlobalTypeName(),
            nodeName ?? string.Empty);
    }

    private static void GenerateRegistration(
        SourceProductionContext context,
        ((ImmutableArray<RootInfo?>, ImmutableArray<ComponentInfo?>), ImmutableArray<ChildInfo?>) combined)
    {
        var (rootsAndComponents, childProps) = combined;
        var (roots, components) = rootsAndComponents;

        var rootList = roots.Where(r => r is not null).Select(r => r!).ToList();
        var componentDict = components
            .Where(c => c is not null)
            .Select(c => c!)
            .ToDictionary(c => c.ClassName);

        var childrenByParent = childProps
            .Where(c => c is not null)
            .Select(c => c!)
            .GroupBy(c => c.ParentClassName)
            .ToDictionary(g => g.Key, g => g.ToList());

        var registrations = new List<RegistrationEntry>();
        var hasError = false;

        foreach (var root in rootList)
        {
            if (!SettingsNameHelper.IsValidPathSegment(root.RootName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.InvalidRootName,
                    Location.None,
                    root.RootName));
                hasError = true;
                continue;
            }

            CollectRegistrations(root.ClassName, root.RootName, componentDict, childrenByParent, registrations);
        }

        var duplicateGroups = registrations
            .GroupBy(r => r.Path)
            .Where(g => g.Count() > 1);

        foreach (var dup in duplicateGroups)
        {
            var first = dup.First();
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.DuplicateSettingsRegistrationPath,
                Location.None,
                first.Path,
                first.SettingsTypeName));
            hasError = true;
        }

        if (hasError) return;
        if (registrations.Count == 0) return;

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine("using ComposableSettings;");
        source.AppendLine();
        source.AppendLine("namespace ComposableSettings.Generated;");
        source.AppendLine();
        source.AppendLine("public  class GeneratedComponentSettingsInitializer : IComponentSettingsInitializer");
        source.AppendLine("{");
        source.AppendLine("    private readonly IComponentSettingsStore _store;");
        source.AppendLine();
        source.AppendLine("    public GeneratedComponentSettingsInitializer(IComponentSettingsStore store)");
        source.AppendLine("    {");
        source.AppendLine("        _store = store;");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public void Initialize(bool resetToDefaults = false)");
        source.AppendLine("    {");

        foreach (var reg in registrations)
            source.AppendLine($"        _store.Register<{reg.SettingsTypeName}>({GeneratePathExpression(reg.Path)});");

        source.AppendLine();
        source.AppendLine("        _store.CompleteRegistration(resetToDefaults);");
        source.AppendLine("    }");
        source.AppendLine("}");

        context.AddSource(
            "GeneratedComponentSettingsInitializer.g.cs",
            SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void CollectRegistrations(
        string className,
        string pathSoFar,
        Dictionary<string, ComponentInfo> componentDict,
        Dictionary<string, List<ChildInfo>> childrenByParent,
        List<RegistrationEntry> registrations)
    {
        if (componentDict.TryGetValue(className, out var component)
            && component.SettingsTypeName is not null)
            registrations.Add(new RegistrationEntry(pathSoFar, component.SettingsTypeName));

        if (!childrenByParent.TryGetValue(className, out var children))
            return;

        foreach (var child in children)
        {
            var childPath = $"{pathSoFar}/{child.NodeName}";
            CollectRegistrations(child.ChildClassName, childPath, componentDict, childrenByParent, registrations);
        }
    }

    private static string GeneratePathExpression(string path)
    {
        var segments = path.Split('/');
        if (segments.Length == 0) return "\"\"";
        if (segments.Length == 1)
            return $"ComposableSettings.SettingsNodePath.Root(\"{EscapeStringLiteral(segments[0])}\")";

        var sb = new StringBuilder();
        sb.Append("ComposableSettings.SettingsNodePath.Root(\"");
        sb.Append(EscapeStringLiteral(segments[0]));
        sb.Append("\")");

        for (var i = 1; i < segments.Length; i++)
        {
            sb.Append(".Child(\"");
            sb.Append(EscapeStringLiteral(segments[i]));
            sb.Append("\")");
        }

        return sb.ToString();
    }

    private static string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private  class RootInfo(string className, string rootName)
    {
        public string ClassName { get; } = className;
        public string RootName { get; } = rootName;
    }

    private  class ComponentInfo(string className, string name, string? settingsTypeName)
    {
        public string ClassName { get; } = className;
        public string Name { get; } = name;
        public string? SettingsTypeName { get; } = settingsTypeName;
    }

    private  class ChildInfo(string parentClassName, string childClassName, string nodeName)
    {
        public string ParentClassName { get; } = parentClassName;
        public string ChildClassName { get; } = childClassName;
        public string NodeName { get; } = nodeName;
    }

    private  class RegistrationEntry(string path, string settingsTypeName)
    {
        public string Path { get; } = path;
        public string SettingsTypeName { get; } = settingsTypeName;
    }
}