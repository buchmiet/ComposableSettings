using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ComposableSettings.Generator.Helpers;

namespace ComposableSettings.Generator.SourceGenerators;

[Generator]
public sealed class SettingsChildrenGenerator : IIncrementalGenerator
{
    private const string GeneratedMethodName = "InitializeGeneratedSettingsChildren";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var childProperties = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is PropertyDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => GetChildProperty(ctx))
            .Where(static property => property is not null);

        var componentClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => GetComponentClass(ctx))
            .Where(static component => component is not null);

        context.RegisterSourceOutput(componentClasses.Collect(), ReportComponentNameDiagnostics);
        context.RegisterSourceOutput(childProperties.Collect(), ProcessChildProperties);
    }

    private static ChildPropertyCandidate? GetChildProperty(GeneratorSyntaxContext context)
    {
        var propertySyntax = (PropertyDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(propertySyntax) is not IPropertySymbol propertySymbol)
            return null;

        if (!propertySymbol.HasAttribute(GeneratorConstants.SettingsChildAttributeFullName))
            return null;

        return new ChildPropertyCandidate(propertySyntax, propertySymbol);
    }

    private static ComponentClassCandidate? GetComponentClass(GeneratorSyntaxContext context)
    {
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
            return null;

        var attribute = classSymbol.GetAttribute(GeneratorConstants.SettingsComponentAttributeFullName);
        if (attribute is null)
            return null;

        var name = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;

        return new ComponentClassCandidate(classSyntax, classSymbol, name);
    }

    private static void ReportComponentNameDiagnostics(
        SourceProductionContext context,
        ImmutableArray<ComponentClassCandidate?> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is null) continue;
            if (!SettingsNameHelper.IsValidPathSegment(candidate.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.InvalidComponentName,
                    GetSettingsComponentNameLocation(candidate.Syntax),
                    candidate.Name ?? "<null>"));
            }
        }
    }

    private static void ProcessChildProperties(
        SourceProductionContext context,
        ImmutableArray<ChildPropertyCandidate?> candidates)
    {
        var properties = candidates
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .GroupBy(candidate => candidate.Property.ContainingType, SymbolEqualityComparer.Default);

        foreach (var parentGroup in properties)
        {
            var parent = (INamedTypeSymbol)parentGroup.Key!;
            var children = parentGroup
                .Select(candidate => BuildChildModel(context, parent, candidate))
                .ToList();

            var hasError = false;

            if (!parent.IsPartial())
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ParentClassMustBePartial,
                    parent.Locations.FirstOrDefault() ?? children.First().Location,
                    parent.Name));
                hasError = true;
            }

            if (parent.ContainingType is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.NestedParentClassNotSupported,
                    parent.Locations.FirstOrDefault() ?? children.First().Location,
                    parent.Name));
                hasError = true;
            }

            var collidingMethod = FindGeneratedMethodCollision(parent);
            if (collidingMethod is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.GeneratedInitializationMethodAlreadyExists,
                    collidingMethod.Locations.FirstOrDefault() ?? children.First().Location,
                    parent.Name));
                hasError = true;
            }

            foreach (var child in children)
            {
                if (child.HasError) hasError = true;
            }

            foreach (var duplicate in children
                         .Where(child => child.NodeName is not null)
                         .GroupBy(child => child.NodeName!)
                         .Where(group => group.Count() > 1))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.DuplicateChildNodeName,
                    duplicate.Skip(1).First().Location,
                    duplicate.Key,
                    parent.Name));
                hasError = true;
            }

            if (hasError) continue;

            context.AddSource(
                $"{parent.ToDisplayString().Replace('.', '_')}.SettingsChildren.g.cs",
                SourceText.From(GenerateSource(parent, children), Encoding.UTF8));
        }
    }

    private static ChildModel BuildChildModel(
        SourceProductionContext context,
        INamedTypeSymbol parent,
        ChildPropertyCandidate candidate)
    {
        var property = candidate.Property;
        var attribute = property.GetAttribute(GeneratorConstants.SettingsChildAttributeFullName);
        var explicitName = attribute?.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;

        var nodeName = explicitName ?? SettingsNameHelper.ResolveComponentName(property.Type);
        var hasError = false;

        if (property.SetMethod is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.ChildPropertyMustBeAssignable,
                candidate.Syntax.Identifier.GetLocation(),
                property.Name));
            hasError = true;
        }

        if (explicitName is not null && !SettingsNameHelper.IsValidPathSegment(explicitName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.InvalidChildName,
                GetSettingsChildNameLocation(candidate.Syntax),
                explicitName));
            hasError = true;
        }

        var componentName = SettingsNameHelper.ResolveComponentName(property.Type);
        if (!SettingsNameHelper.IsValidPathSegment(componentName))
            hasError = true;

        return new ChildModel(
            property.Name,
            property.Type.ToGlobalTypeName(),
            explicitName,
            nodeName,
            candidate.Syntax.Identifier.GetLocation(),
            hasError);
    }

    private static string GenerateSource(INamedTypeSymbol parent, IReadOnlyList<ChildModel> children)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();

        var namespaceName = parent.GetNamespaceName();
        if (!string.IsNullOrEmpty(namespaceName))
        {
            source.AppendLine($"namespace {namespaceName};");
            source.AppendLine();
        }

        source.AppendLine($"{parent.GetTypeModifiersText()} {parent.Name}");
        source.AppendLine("{");
        source.AppendLine($"    private void {GeneratedMethodName}(");
        source.AppendLine("        global::ComposableSettings.ISettingsNodeFactory factory,");
        source.AppendLine("        global::ComposableSettings.SettingsNodePath parentPath)");
        source.AppendLine("    {");

        foreach (var child in children)
        {
            if (child.ExplicitName is null)
            {
                source.AppendLine($"        {child.PropertyName} = factory.CreateChild<{child.TypeName}>(");
                source.AppendLine("            parentPath);");
            }
            else
            {
                source.AppendLine($"        {child.PropertyName} = factory.CreateChild<{child.TypeName}>(");
                source.AppendLine("            parentPath,");
                source.AppendLine($"            \"{EscapeStringLiteral(child.ExplicitName)}\");");
            }
        }

        source.AppendLine("    }");
        source.AppendLine("}");

        return source.ToString();
    }

    private static string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static IMethodSymbol? FindGeneratedMethodCollision(INamedTypeSymbol parent)
    {
        return parent.GetMembers(GeneratedMethodName)
            .OfType<IMethodSymbol>()
            .FirstOrDefault(IsGeneratedMethodSignature);
    }

    private static bool IsGeneratedMethodSignature(IMethodSymbol method)
    {
        if (method.Parameters.Length != 2) return false;

        return IsType(method.Parameters[0].Type, "ComposableSettings.ISettingsNodeFactory")
            && IsType(method.Parameters[1].Type, "ComposableSettings.SettingsNodePath");
    }

    private static bool IsType(ITypeSymbol type, string metadataName)
    {
        var display = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return display == $"global::{metadataName}"
            || display == metadataName
            || display.EndsWith($".{metadataName}", System.StringComparison.Ordinal);
    }

    private static Location GetSettingsChildNameLocation(PropertyDeclarationSyntax property)
    {
        return property.AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(a => a.Name.ToString().EndsWith("SettingsChild", System.StringComparison.Ordinal)
                || a.Name.ToString().EndsWith("SettingsChildAttribute", System.StringComparison.Ordinal))
            .Select(a => a.ArgumentList?.Arguments.FirstOrDefault()?.GetLocation())
            .FirstOrDefault(location => location is not null)
            ?? property.Identifier.GetLocation();
    }

    private static Location GetSettingsComponentNameLocation(ClassDeclarationSyntax type)
    {
        return type.AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(a => a.Name.ToString().EndsWith("SettingsComponent", System.StringComparison.Ordinal)
                || a.Name.ToString().EndsWith("SettingsComponentAttribute", System.StringComparison.Ordinal))
            .Select(a => a.ArgumentList?.Arguments.FirstOrDefault()?.GetLocation())
            .FirstOrDefault(location => location is not null)
            ?? type.Identifier.GetLocation();
    }

    private sealed class ChildPropertyCandidate(PropertyDeclarationSyntax syntax, IPropertySymbol property)
    {
        public PropertyDeclarationSyntax Syntax { get; } = syntax;
        public IPropertySymbol Property { get; } = property;
    }

    private sealed class ComponentClassCandidate(ClassDeclarationSyntax syntax, INamedTypeSymbol symbol, string? name)
    {
        public ClassDeclarationSyntax Syntax { get; } = syntax;
        public INamedTypeSymbol Symbol { get; } = symbol;
        public string? Name { get; } = name;
    }

    private sealed class ChildModel(
        string propertyName,
        string typeName,
        string? explicitName,
        string? nodeName,
        Location location,
        bool hasError)
    {
        public string PropertyName { get; } = propertyName;
        public string TypeName { get; } = typeName;
        public string? ExplicitName { get; } = explicitName;
        public string? NodeName { get; } = nodeName;
        public Location Location { get; } = location;
        public bool HasError { get; } = hasError;
    }
}
