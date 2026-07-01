using System.Collections.Immutable;
using ComposableSettings.Generator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComposableSettings.Generator.SourceGenerators;

/// <summary>
/// Cross-cutting diagnostics for <see cref="SettingsProxyAttribute"/> and
/// <see cref="SettingsDraftRootAttribute"/> usage.
/// </summary>
[Generator]
public class SettingsProxyValidationGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor OrphanSettingsProxy = new(
        "CSP046",
        "SettingsProxy requires SettingsVm or SettingsDraftVm",
        "[SettingsProxy] on '{0}.{1}' is not on a class marked [SettingsVm] or [SettingsDraftVm]; the proxy will not be generated",
        "ComposableSettings",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor OrphanSettingsDraftRoot = new(
        "CSP048",
        "SettingsDraftRoot requires SettingsDraftVm",
        "[SettingsDraftRoot] on '{0}' has no matching [SettingsDraftVm]; the root path will be ignored",
        "ComposableSettings",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var proxyProperties = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is PropertyDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => GetOrphanProxy(ctx))
            .Where(static c => c is not null);

        var draftRootClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => GetOrphanDraftRoot(ctx))
            .Where(static c => c is not null);

        context.RegisterSourceOutput(proxyProperties.Collect(), ReportOrphanProxies);
        context.RegisterSourceOutput(draftRootClasses.Collect(), ReportOrphanDraftRoots);
    }

    private static OrphanProxy? GetOrphanProxy(GeneratorSyntaxContext context)
    {
        if (context.Node is not PropertyDeclarationSyntax propertySyntax)
            return null;

        if (context.SemanticModel.GetDeclaredSymbol(propertySyntax) is not IPropertySymbol propertySymbol)
            return null;

        if (!propertySymbol.HasAttribute(GeneratorConstants.SettingsProxyAttributeFullName))
            return null;

        if (propertySyntax.Parent is not ClassDeclarationSyntax classSyntax)
            return null;

        if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
            return null;

        if (classSymbol.HasAttribute(GeneratorConstants.ObservableSettingsAttributeFullName)
            || classSymbol.HasAttribute(GeneratorConstants.SettingsDraftVmAttributeFullName))
        {
            return null;
        }

        return new OrphanProxy(
            propertySyntax.Identifier.GetLocation(),
            classSymbol.Name,
            propertySymbol.Name);
    }

    private static OrphanDraftRoot? GetOrphanDraftRoot(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classSyntax)
            return null;

        if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
            return null;

        if (!classSymbol.HasAttribute(GeneratorConstants.SettingsDraftRootAttributeFullName))
            return null;

        if (classSymbol.HasAttribute(GeneratorConstants.SettingsDraftVmAttributeFullName))
            return null;

        return new OrphanDraftRoot(classSyntax.Identifier.GetLocation(), classSymbol.Name);
    }

    private static void ReportOrphanProxies(SourceProductionContext context, ImmutableArray<OrphanProxy?> proxies)
    {
        foreach (var proxy in proxies)
        {
            if (proxy is null)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                OrphanSettingsProxy,
                proxy.Location,
                proxy.ClassName,
                proxy.PropertyName));
        }
    }

    private static void ReportOrphanDraftRoots(SourceProductionContext context, ImmutableArray<OrphanDraftRoot?> classes)
    {
        foreach (var orphan in classes)
        {
            if (orphan is null)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                OrphanSettingsDraftRoot,
                orphan.Location,
                orphan.ClassName));
        }
    }

    private  class OrphanProxy(Location location, string className, string propertyName)
    {
        public Location Location { get; } = location;
        public string ClassName { get; } = className;
        public string PropertyName { get; } = propertyName;
    }

    private  class OrphanDraftRoot(Location location, string className)
    {
        public Location Location { get; } = location;
        public string ClassName { get; } = className;
    }
}
