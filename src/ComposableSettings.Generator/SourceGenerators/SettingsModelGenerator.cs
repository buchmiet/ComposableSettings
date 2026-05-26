using System;
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
/// Turns a <c>[SettingsModel] partial class</c> into an observable settings model:
/// emits <see cref="System.ComponentModel.INotifyPropertyChanged"/> plus, for each
/// instance field named <c>_camelCase</c>:
///   - scalar field  -> public change-raising property (default from field initializer),
///   - ObservableCollection field -> get-only property + a generated constructor that
///     bridges CollectionChanged to PropertyChanged (so the provider auto-persists on add/remove).
/// </summary>
[Generator]
public class SettingsModelGenerator : IIncrementalGenerator
{
    private const string ObservableCollectionDefinition = "System.Collections.ObjectModel.ObservableCollection<T>";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "CSP020",
        "Settings model must be partial",
        "[SettingsModel] class '{0}' must be declared 'partial' for INotifyPropertyChanged generation",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AlreadyNotifies = new(
        "CSP021",
        "Settings model already implements INotifyPropertyChanged",
        "[SettingsModel] class '{0}' already implements INotifyPropertyChanged; remove the base/interface and let the generator provide it",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CtorNotAllowedWithCollections = new(
        "CSP025",
        "Settings model with collections must not declare a constructor",
        "[SettingsModel] class '{0}' has observable collection fields, so the generator owns the constructor; remove the user-defined constructor",
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
        return symbol.HasAttribute(GeneratorConstants.SettingsModelAttributeFullName)
            ? new Candidate(syntax, symbol)
            : null;
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<Candidate?> candidates)
    {
        foreach (var candidate in candidates)
            if (candidate is not null)
                GenerateOne(context, candidate);
    }

    private static void GenerateOne(SourceProductionContext context, Candidate candidate)
    {
        var symbol = candidate.Symbol;

        if (!symbol.IsPartial())
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, candidate.Syntax.Identifier.GetLocation(), symbol.Name));
            return;
        }

        if (symbol.AllInterfaces.Any(i => i.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged"))
        {
            context.ReportDiagnostic(Diagnostic.Create(AlreadyNotifies, candidate.Syntax.Identifier.GetLocation(), symbol.Name));
            return;
        }

        var scalars = new List<FieldModel>();
        var collections = new List<FieldModel>();

        foreach (var field in symbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsImplicitlyDeclared || field.IsStatic || field.IsConst || field.AssociatedSymbol is not null)
                continue;
            if (!field.Name.StartsWith("_", StringComparison.Ordinal) || field.Name.TrimStart('_').Length == 0)
                continue;

            var propertyName = ToPropertyName(field.Name);
            if (symbol.GetMembers(propertyName).OfType<IPropertySymbol>().Any())
                continue;

            var model = new FieldModel(field.Name, propertyName, field.Type.ToGlobalTypeName());
            if (IsObservableCollection(field.Type))
                collections.Add(model);
            else if (!field.IsReadOnly)
                scalars.Add(model);
            // readonly scalar -> skipped (no settable property to generate)
        }

        if (scalars.Count == 0 && collections.Count == 0)
            return;

        if (collections.Count > 0 && HasUserConstructor(symbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(CtorNotAllowedWithCollections, candidate.Syntax.Identifier.GetLocation(), symbol.Name));
            return;
        }

        context.AddSource(
            $"{symbol.ToDisplayString().Replace('.', '_')}.SettingsModel.g.cs",
            SourceText.From(BuildSource(symbol, scalars, collections), Encoding.UTF8));
    }

    private static bool IsObservableCollection(ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true } named
           && named.ConstructedFrom.ToDisplayString() == ObservableCollectionDefinition;

    private static bool HasUserConstructor(INamedTypeSymbol type)
        => type.InstanceConstructors.Any(ctor => !ctor.IsImplicitlyDeclared);

    private static string ToPropertyName(string fieldName)
    {
        var trimmed = fieldName.TrimStart('_');
        return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
    }

    private static string BuildSource(
        INamedTypeSymbol type,
        IReadOnlyList<FieldModel> scalars,
        IReadOnlyList<FieldModel> collections)
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

        sb.AppendLine($"{accessibility} partial class {type.Name} : global::System.ComponentModel.INotifyPropertyChanged");
        sb.AppendLine("{");
        sb.AppendLine("    public event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;");
        sb.AppendLine();

        // Constructor (only when collections exist) bridges CollectionChanged -> PropertyChanged
        // so that adds/removes auto-persist through the provider.
        if (collections.Count > 0)
        {
            sb.AppendLine($"    public {type.Name}()");
            sb.AppendLine("    {");
            foreach (var collection in collections)
            {
                sb.AppendLine($"        {collection.FieldName}.CollectionChanged += (_, _) =>");
                sb.AppendLine($"            PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(nameof({collection.PropertyName})));");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (var field in scalars)
        {
            sb.AppendLine($"    public {field.TypeName} {field.PropertyName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => {field.FieldName};");
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (!global::System.Collections.Generic.EqualityComparer<{field.TypeName}>.Default.Equals({field.FieldName}, value))");
            sb.AppendLine("            {");
            sb.AppendLine($"                {field.FieldName} = value;");
            sb.AppendLine($"                PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(nameof({field.PropertyName})));");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (var collection in collections)
        {
            // Get-only: mutate in place (Add/Remove); replacement is not supported.
            sb.AppendLine($"    public {collection.TypeName} {collection.PropertyName} => {collection.FieldName};");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private sealed class Candidate(ClassDeclarationSyntax syntax, INamedTypeSymbol symbol)
    {
        public ClassDeclarationSyntax Syntax { get; } = syntax;
        public INamedTypeSymbol Symbol { get; } = symbol;
    }

    private sealed class FieldModel(string fieldName, string propertyName, string typeName)
    {
        public string FieldName { get; } = fieldName;
        public string PropertyName { get; } = propertyName;
        public string TypeName { get; } = typeName;
    }
}
