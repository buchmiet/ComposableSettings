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
/// Turns an <c>[ObservableSettings(typeof(TSettings))] partial class</c> that ALREADY
/// implements INPC (e.g. derives from CommunityToolkit <c>ObservableObject</c>) into a
/// settings consumer that stores NO settings state. It emits:
/// <list type="bullet">
///   <item>a provider field + a pass-through <c>Settings</c> property,</item>
///   <item><c>InitializeSettings(provider)</c> (called from the user's own ctor),</item>
///   <item>a relay that, on every model change, calls the class's existing
///   <c>OnPropertyChanged(string)</c> for <c>Settings</c>, the changed member, and a
///   user-overridable <c>partial void OnSettingsMemberChanged(string?)</c> for derived
///   properties,</item>
///   <item>the implementing parts of any <c>[SettingsProxy]</c> partial properties,
///   forwarding get/set to <c>Settings.X</c>.</item>
/// </list>
/// Unlike <see cref="SettingsConsumerGenerator"/>, this does NOT emit its own
/// <c>PropertyChanged</c> event — it relays into the host's INPC — so it does not
/// conflict with ObservableObject (no CSP024).
/// </summary>
[Generator]
public class ObservableSettingsGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "CSP030",
        "ObservableSettings consumer must be partial",
        "[ObservableSettings] class '{0}' must be declared 'partial'",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingSettingsType = new(
        "CSP031",
        "ObservableSettings requires a settings type",
        "[ObservableSettings] on '{0}' requires a settings type argument",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ProxyPropertyMissing = new(
        "CSP032",
        "Settings proxy has no matching settings property",
        "[SettingsProxy] property '{0}' on '{2}' has no matching public property on settings type '{1}'",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ProxyTypeMismatch = new(
        "CSP033",
        "Settings proxy type does not match settings property",
        "[SettingsProxy] property '{0}' on '{3}' has type '{1}' but the settings property has type '{2}'",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

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

        var attribute = symbol.GetAttribute(GeneratorConstants.ObservableSettingsAttributeFullName);
        if (attribute is null)
            return null;

        var settingsType = attribute.ConstructorArguments.Length > 0
                           && attribute.ConstructorArguments[0].Value is INamedTypeSymbol t
            ? t
            : null;

        var proxies = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.HasAttribute(GeneratorConstants.SettingsProxyAttributeFullName))
            .ToList();

        return new Candidate(syntax, symbol, settingsType, proxies);
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

        if (candidate.SettingsType is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingSettingsType, candidate.Syntax.Identifier.GetLocation(), symbol.Name));
            return;
        }

        if (!symbol.IsPartial())
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, candidate.Syntax.Identifier.GetLocation(), symbol.Name));
            return;
        }

        var validProxies = ValidateProxies(context, candidate);
        context.AddSource(
            $"{symbol.ToDisplayString().Replace('.', '_')}.ObservableSettings.g.cs",
            SourceText.From(BuildSource(symbol, candidate.SettingsType.ToGlobalTypeName(), validProxies), Encoding.UTF8));
    }

    private static List<ProxyInfo> ValidateProxies(SourceProductionContext context, Candidate candidate)
    {
        var settingsType = candidate.SettingsType!;
        var validProxies = new List<ProxyInfo>();

        foreach (var proxy in candidate.Proxies)
        {
            var location = GetProxyDiagnosticLocation(proxy);

            if (!SettingsModelMemberResolver.TryResolveMemberType(settingsType, proxy.Name, out var settingsMemberType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ProxyPropertyMissing,
                    location,
                    proxy.Name,
                    settingsType.Name,
                    candidate.Symbol.Name));
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(proxy.Type, settingsMemberType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ProxyTypeMismatch,
                    location,
                    proxy.Name,
                    proxy.Type.ToDisplayString(),
                    settingsMemberType.ToDisplayString(),
                    candidate.Symbol.Name));
                continue;
            }

            validProxies.Add(new ProxyInfo(proxy.Name, proxy.Type.ToGlobalTypeName()));
        }

        return validProxies;
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

    private static string BuildSource(INamedTypeSymbol type, string settingsType, List<ProxyInfo> proxies)
    {
        var ns = type.GetNamespaceName();
        var accessibility = type.GetAccessibilityText();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        if (ns.Length > 0)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"{accessibility} partial class {type.Name}");
        sb.AppendLine("{");
        sb.AppendLine($"    private global::ComposableSettings.ISettingsProvider<{settingsType}> _settingsProvider = null!;");
        sb.AppendLine($"    private {settingsType}? _hookedSettings;");
        sb.AppendLine("    private bool _generatedSettingsDisposed;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Live, shared settings instance. The component stores no copy.</summary>");
        sb.AppendLine($"    public {settingsType} Settings => _settingsProvider.Current;");
        sb.AppendLine();
        sb.AppendLine($"    private void InitializeSettings(global::ComposableSettings.ISettingsProvider<{settingsType}> provider)");
        sb.AppendLine("    {");
        sb.AppendLine("        _settingsProvider = provider;");
        sb.AppendLine("        RehookGeneratedSettings();");
        sb.AppendLine("        provider.Replaced += OnSettingsProviderReplaced;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    private void OnSettingsProviderReplaced(object? sender, {settingsType} e)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_generatedSettingsDisposed)");
        sb.AppendLine("            return;");
        sb.AppendLine("        RehookGeneratedSettings();");
        sb.AppendLine("        RaiseAllSettings();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"{accessibility} void DisposeGeneratedSettings()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_generatedSettingsDisposed)");
        sb.AppendLine("            return;");
        sb.AppendLine("        _generatedSettingsDisposed = true;");
        sb.AppendLine("        _settingsProvider.Replaced -= OnSettingsProviderReplaced;");
        sb.AppendLine("        if (_hookedSettings is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            _hookedSettings.PropertyChanged -= OnGeneratedSettingsChanged;");
        sb.AppendLine("            _hookedSettings = null;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void RehookGeneratedSettings()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_generatedSettingsDisposed)");
        sb.AppendLine("            return;");
        sb.AppendLine("        if (_hookedSettings is not null)");
        sb.AppendLine("            _hookedSettings.PropertyChanged -= OnGeneratedSettingsChanged;");
        sb.AppendLine("        _hookedSettings = _settingsProvider.Current;");
        sb.AppendLine("        _hookedSettings.PropertyChanged += OnGeneratedSettingsChanged;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void OnGeneratedSettingsChanged(object? sender, global::System.ComponentModel.PropertyChangedEventArgs e)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_generatedSettingsDisposed)");
        sb.AppendLine("            return;");
        sb.AppendLine("        OnPropertyChanged(nameof(Settings));");
        sb.AppendLine("        if (!string.IsNullOrEmpty(e.PropertyName))");
        sb.AppendLine("            OnPropertyChanged(e.PropertyName!);");
        sb.AppendLine("        OnSettingsMemberChanged(e.PropertyName);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void RaiseAllSettings()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_generatedSettingsDisposed)");
        sb.AppendLine("            return;");
        sb.AppendLine("        OnPropertyChanged(nameof(Settings));");
        foreach (var proxy in proxies)
            sb.AppendLine($"        OnPropertyChanged(\"{proxy.Name}\");");
        sb.AppendLine("        OnSettingsMemberChanged(null);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Optional hook to refresh DERIVED properties (e.g. labels) on a settings change.</summary>");
        sb.AppendLine("    partial void OnSettingsMemberChanged(string? propertyName);");

        foreach (var proxy in proxies)
        {
            sb.AppendLine();
            sb.AppendLine($"    public partial {proxy.Type} {proxy.Name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => Settings.{proxy.Name};");
            sb.AppendLine($"        set => Settings.{proxy.Name} = value;");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private sealed class Candidate(
        ClassDeclarationSyntax syntax,
        INamedTypeSymbol symbol,
        INamedTypeSymbol? settingsType,
        List<IPropertySymbol> proxies)
    {
        public ClassDeclarationSyntax Syntax { get; } = syntax;
        public INamedTypeSymbol Symbol { get; } = symbol;
        public INamedTypeSymbol? SettingsType { get; } = settingsType;
        public List<IPropertySymbol> Proxies { get; } = proxies;
    }

    private sealed class ProxyInfo(string name, string type)
    {
        public string Name { get; } = name;
        public string Type { get; } = type;
    }
}
