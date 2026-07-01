using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using ComposableSettings.Generator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ComposableSettings.Generator.SourceGenerators;

/// <summary>
/// Turns an <c>[SettingsDraftVm(typeof(TDocument))] partial class</c> into a document-profile
/// settings editor consumer: draft accessors, preview on change, INPC relay.
/// </summary>
[Generator]
public class SettingsDraftVmGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "CSP044",
        "SettingsDraftVm consumer must be partial",
        "[SettingsDraftVm] class '{0}' must be declared 'partial'",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingDocumentType = new(
        "CSP045",
        "SettingsDraftVm requires a document type",
        "[SettingsDraftVm] on '{0}' requires a document type argument",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ConflictsWithSettingsVm = GeneratorDiagnostics.ConflictsWithSettingsDraftVm;

    private static readonly DiagnosticDescriptor ProxyPathMissing = new(
        "CSP042",
        "Settings proxy path not found on document",
        "[SettingsProxy] path '{0}' on '{2}' has no matching member on document type '{1}'",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ProxyTypeMismatch = new(
        "CSP043",
        "Settings proxy type does not match document member",
        "[SettingsProxy] property '{0}' on '{3}' has type '{1}' but the document member has type '{2}'",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ProxyMustBePartial = GeneratorDiagnostics.ProxyMustBePartial;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => GetCandidate(ctx))
            .Where(static c => c is not null);

        context.RegisterSourceOutput(candidates.Collect(), Generate);
    }

    private static Candidate? GetCandidate(GeneratorSyntaxContext context)
    {
        var syntax = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(syntax) is not INamedTypeSymbol symbol)
            return null;

        var attribute = symbol.GetAttribute(GeneratorConstants.SettingsDraftVmAttributeFullName);
        if (attribute is null)
            return null;

        if (symbol.HasAttribute(GeneratorConstants.ObservableSettingsAttributeFullName))
            return new Candidate(syntax, symbol, null, null, [], hasSettingsVmConflict: true);

        var documentType = attribute.ConstructorArguments.Length > 0
                           && attribute.ConstructorArguments[0].Value is INamedTypeSymbol t
            ? t
            : null;

        var draftRoot = symbol.GetAttribute(GeneratorConstants.SettingsDraftRootAttributeFullName);
        var rootPath = draftRoot?.ConstructorArguments.Length > 0
                       && draftRoot.ConstructorArguments[0].Value is string path
            ? path.Trim()
            : string.Empty;

        var proxies = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.HasAttribute(GeneratorConstants.SettingsProxyAttributeFullName))
            .ToList();

        return new Candidate(syntax, symbol, documentType, rootPath, proxies, hasSettingsVmConflict: false);
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<Candidate?> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is not null)
                GenerateOne(context, candidate);
        }
    }

    private static void GenerateOne(SourceProductionContext context, Candidate candidate)
    {
        var symbol = candidate.Symbol;

        if (candidate.HasSettingsVmConflict)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ConflictsWithSettingsVm,
                candidate.Syntax.Identifier.GetLocation(),
                symbol.Name));
            return;
        }

        if (candidate.DocumentType is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingDocumentType,
                candidate.Syntax.Identifier.GetLocation(),
                symbol.Name));
            return;
        }

        if (!symbol.IsPartial())
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MustBePartial,
                candidate.Syntax.Identifier.GetLocation(),
                symbol.Name));
            return;
        }

        var validProxies = ValidateProxies(context, candidate);
        context.AddSource(
            $"{symbol.ToDisplayString().Replace('.', '_')}.SettingsDraftVm.g.cs",
            SourceText.From(BuildSource(symbol, candidate.DocumentType.ToGlobalTypeName(), validProxies), Encoding.UTF8));
    }

    private static List<ProxyInfo> ValidateProxies(SourceProductionContext context, Candidate candidate)
    {
        var documentType = candidate.DocumentType!;
        var validProxies = new List<ProxyInfo>();

        foreach (var proxy in candidate.Proxies)
        {
            var location = GetProxyDiagnosticLocation(proxy);

            if (!proxy.IsPartialProperty())
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ProxyMustBePartial,
                    location,
                    proxy.Name,
                    candidate.Symbol.Name));
                continue;
            }

            var memberPath = ResolveProxyMemberPath(proxy, candidate.DraftRootPath);
            if (!DocumentMemberPathResolver.TryResolvePath(documentType, memberPath, out var leafType, out var accessorSuffix))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ProxyPathMissing,
                    location,
                    memberPath,
                    documentType.Name,
                    candidate.Symbol.Name));
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(proxy.Type, leafType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ProxyTypeMismatch,
                    location,
                    proxy.Name,
                    proxy.Type.ToDisplayString(),
                    leafType.ToDisplayString(),
                    candidate.Symbol.Name));
                continue;
            }

            validProxies.Add(new ProxyInfo(
                proxy.Name.EscapeIdentifier(),
                proxy.Type.ToGlobalTypeName(),
                memberPath,
                accessorSuffix,
                leafType.SpecialType == SpecialType.System_Double));
        }

        return validProxies;
    }

    private static string ResolveProxyMemberPath(IPropertySymbol proxy, string draftRootPath)
    {
        var attribute = proxy.GetAttribute(GeneratorConstants.SettingsProxyAttributeFullName);
        string relativePath;
        if (attribute?.ConstructorArguments.Length > 0
            && attribute.ConstructorArguments[0].Value is string explicitPath
            && !string.IsNullOrWhiteSpace(explicitPath))
        {
            relativePath = explicitPath.Trim();
        }
        else
        {
            relativePath = proxy.Name;
        }

        if (string.IsNullOrEmpty(draftRootPath))
            return relativePath;

        return string.IsNullOrEmpty(relativePath)
            ? draftRootPath
            : $"{draftRootPath}.{relativePath}";
    }

    private static Location GetProxyDiagnosticLocation(IPropertySymbol proxy)
    {
        if (proxy.Locations is { Length: > 0 } locations && locations[0].IsInSource)
            return locations[0];

        foreach (var reference in proxy.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is PropertyDeclarationSyntax propertySyntax)
                return propertySyntax.Identifier.GetLocation();
        }

        return Location.None;
    }

    private static string BuildSource(
        INamedTypeSymbol type,
        string documentType,
        List<ProxyInfo> proxies)
    {
        var accessibility = type.GetAccessibilityText();
        var sb = GeneratedSourceBuilder.Begin(type.GetNamespaceName());

        using (sb.Block($"{accessibility} partial class {type.Name.EscapeIdentifier()}"))
        {
            sb.AddProperty($"private global::ComposableSettings.Document.SettingsEditingSession<{documentType}> _draftSession", "null!");
            sb.AddProperty($"private global::ComposableSettings.Document.ISettingsDocumentStore<{documentType}> _draftStore", "null!");
            sb.AddProperty("private bool _draftVmDisposed");
            sb.AppendLine();
            sb.AppendLine($"private {documentType} Draft => _draftSession.Draft;");
            sb.AppendLine();

            sb.AppendLine("private void InitializeSettingsDraft(");
            sb.AppendLine($"    global::ComposableSettings.Document.SettingsEditingSession<{documentType}> session,");
            sb.AppendLine($"    global::ComposableSettings.Document.ISettingsDocumentStore<{documentType}> store)");
            using (sb.Block(""))
            {
                sb.AppendLine("_draftSession = session;");
                sb.AppendLine("_draftStore = store;");
                sb.AppendLine("store.EffectiveChanged += OnStoreEffectiveChanged;");
            }

            sb.AppendLine();

            using (sb.Block($"{accessibility} void DisposeGeneratedSettingsDraft()"))
            {
                using (sb.Block("if (_draftVmDisposed)"))
                    sb.AppendLine("return;");

                sb.AppendLine("_draftVmDisposed = true;");
                sb.AppendLine("_draftStore.EffectiveChanged -= OnStoreEffectiveChanged;");
            }

            sb.AppendLine();

            using (sb.Block("private void OnStoreEffectiveChanged(object? sender, global::System.EventArgs e)"))
            {
                using (sb.Block("if (_draftVmDisposed)"))
                    sb.AppendLine("return;");

                sb.AppendLine("RefreshAllDraftProxies();");
            }

            sb.AppendLine();

            using (sb.Block("private void RefreshAllDraftProxies()"))
            {
                using (sb.Block("if (_draftVmDisposed)"))
                    sb.AppendLine("return;");

                foreach (var proxy in proxies)
                    sb.AppendLine($"OnPropertyChanged(nameof({proxy.Name}));");

                sb.AppendLine("OnDraftMemberChanged(null);");
            }

            sb.AppendLine();

            using (sb.Block("private void OnDraftPropertyChanged(string memberPath)"))
            {
                using (sb.Block("if (_draftVmDisposed)"))
                    sb.AppendLine("return;");

                sb.AppendLine("_draftSession.Touch();");
                sb.AppendLine("_draftStore.Preview(_draftSession.Draft);");
                sb.AppendLine("OnDraftMemberChanged(memberPath);");
            }

            sb.AppendLine();
            sb.WriteSummary("Optional hook after draft preview (e.g. derived labels).");
            sb.AppendLine("partial void OnDraftMemberChanged(string? memberPath);");

            foreach (var proxy in proxies)
                EmitProxyProperty(sb, proxy);
        }

        return sb.ToString();
    }

    private static void EmitProxyProperty(ComposableSettings.Generator.IndentedStringBuilder.IndentedStringBuilder sb, ProxyInfo proxy)
    {
        var accessor = $"Draft.{proxy.AccessorSuffix}";
        var memberPathLiteral = proxy.MemberPath.Replace("\\", "\\\\").Replace("\"", "\\\"");

        sb.AppendLine();
        using (sb.Block($"public partial {proxy.Type} {proxy.Name}"))
        {
            sb.AppendLine($"get => {accessor};");
            using (sb.Block("set"))
            {
                if (proxy.IsDouble)
                {
                    sb.AppendLine("if (!global::ComposableSettings.Document.DraftMutation.TrySetDouble(");
                    sb.AppendLine($"        {accessor},");
                    sb.AppendLine("        value,");
                    sb.AppendLine($"        v => {accessor} = v,");
                    sb.AppendLine($"        () => OnDraftPropertyChanged(\"{memberPathLiteral}\")))");
                }
                else
                {
                    sb.AppendLine("if (!global::ComposableSettings.Document.DraftMutation.TrySet(");
                    sb.AppendLine($"        {accessor},");
                    sb.AppendLine("        value,");
                    sb.AppendLine($"        v => {accessor} = v,");
                    sb.AppendLine($"        () => OnDraftPropertyChanged(\"{memberPathLiteral}\")))");
                }

                sb.AppendLine("    return;");
                sb.AppendLine($"OnPropertyChanged(nameof({proxy.Name}));");
            }
        }
    }

    private  class Candidate(
        ClassDeclarationSyntax syntax,
        INamedTypeSymbol symbol,
        INamedTypeSymbol? documentType,
        string? draftRootPath,
        List<IPropertySymbol> proxies,
        bool hasSettingsVmConflict)
    {
        public ClassDeclarationSyntax Syntax { get; } = syntax;
        public INamedTypeSymbol Symbol { get; } = symbol;
        public INamedTypeSymbol? DocumentType { get; } = documentType;
        public string DraftRootPath { get; } = draftRootPath ?? string.Empty;
        public List<IPropertySymbol> Proxies { get; } = proxies;
        public bool HasSettingsVmConflict { get; } = hasSettingsVmConflict;
    }

    private  class ProxyInfo(
        string name,
        string type,
        string memberPath,
        string accessorSuffix,
        bool isDouble)
    {
        public string Name { get; } = name;
        public string Type { get; } = type;
        public string MemberPath { get; } = memberPath;
        public string AccessorSuffix { get; } = accessorSuffix;
        public bool IsDouble { get; } = isDouble;
    }
}
