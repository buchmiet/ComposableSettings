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
/// Turns a <c>[SettingsModel] partial class</c> into an observable settings model.
/// For each instance field named <c>_camelCase</c>:
///   - scalar (string / value type / enum) -> change-raising property,
///   - ObservableCollection&lt;T&gt; -> get-only property, tracked (add/remove + item edits),
///   - other reference type (nested model) -> property whose setter re-tracks the child,
///     and which is tracked from the constructor.
/// Nested changes at any depth propagate to PropertyChanged, so the provider auto-persists.
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

    private static readonly DiagnosticDescriptor CtorNotAllowed = new(
        "CSP025",
        "Settings model with tracked members must not declare a constructor",
        "[SettingsModel] class '{0}' has collection or nested-object fields, so the generator owns the constructor; remove the user-defined constructor",
        "ComposableSettings",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ReadOnlyChildNotAllowed = new(
        "CSP026",
        "Nested-object settings field must not be readonly",
        "[SettingsModel] class '{0}' has a readonly nested-object field '{1}'; the generator owns its setter — make the field non-readonly (readonly is fine for collections)",
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

        var fields = new List<FieldModel>();
        foreach (var field in symbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsImplicitlyDeclared || field.IsStatic || field.IsConst || field.AssociatedSymbol is not null)
                continue;
            if (!field.Name.StartsWith("_", StringComparison.Ordinal) || field.Name.TrimStart('_').Length == 0)
                continue;

            var propertyName = ToPropertyName(field.Name);
            if (symbol.GetMembers(propertyName).OfType<IPropertySymbol>().Any())
                continue;

            var kind = Classify(field);
            if (kind == FieldKind.ScalarReadOnly)
                continue; // readonly scalar -> nothing settable to generate
            if (kind == FieldKind.ChildReadOnly)
            {
                context.ReportDiagnostic(Diagnostic.Create(ReadOnlyChildNotAllowed, candidate.Syntax.Identifier.GetLocation(), symbol.Name, field.Name));
                return;
            }

            fields.Add(new FieldModel(field.Name, propertyName, field.Type.ToGlobalTypeName(), kind));
        }

        if (fields.Count == 0)
            return;

        var needsConstructor = fields.Any(f => f.Kind is FieldKind.Collection or FieldKind.Child);
        if (needsConstructor && HasUserConstructor(symbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(CtorNotAllowed, candidate.Syntax.Identifier.GetLocation(), symbol.Name));
            return;
        }

        context.AddSource(
            $"{symbol.ToDisplayString().Replace('.', '_')}.SettingsModel.g.cs",
            SourceText.From(BuildSource(symbol, fields, needsConstructor), Encoding.UTF8));
    }

    private static FieldKind Classify(IFieldSymbol field)
    {
        if (IsObservableCollection(field.Type))
            return FieldKind.Collection;
        if (field.Type.SpecialType == SpecialType.System_String || field.Type.IsValueType)
            return field.IsReadOnly ? FieldKind.ScalarReadOnly : FieldKind.Scalar;
        return field.IsReadOnly ? FieldKind.ChildReadOnly : FieldKind.Child; // other reference type -> nested object
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

    private static string BuildSource(INamedTypeSymbol type, IReadOnlyList<FieldModel> fields, bool needsConstructor)
    {
        const string pcea = "global::System.ComponentModel.PropertyChangedEventArgs";
        const string track = "global::ComposableSettings.SettingsChangeTracking";

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

        if (needsConstructor)
        {
            sb.AppendLine($"    public {type.Name}()");
            sb.AppendLine("    {");
            foreach (var f in fields)
            {
                if (f.Kind == FieldKind.Collection)
                    sb.AppendLine($"        {track}.TrackCollection({f.FieldName}, () => PropertyChanged?.Invoke(this, new {pcea}(nameof({f.PropertyName}))));");
                else if (f.Kind == FieldKind.Child)
                    sb.AppendLine($"        {track}.Track({f.FieldName}, {f.PropertyName}__OnChildChanged);");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (var f in fields)
        {
            switch (f.Kind)
            {
                case FieldKind.Scalar:
                    sb.AppendLine($"    public {f.TypeName} {f.PropertyName}");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        get => {f.FieldName};");
                    sb.AppendLine("        set");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            if (!global::System.Collections.Generic.EqualityComparer<{f.TypeName}>.Default.Equals({f.FieldName}, value))");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                {f.FieldName} = value;");
                    sb.AppendLine($"                PropertyChanged?.Invoke(this, new {pcea}(nameof({f.PropertyName})));");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                    break;

                case FieldKind.Collection:
                    // Get-only: mutate in place (add/remove). Tracking is wired in the constructor.
                    sb.AppendLine($"    public {f.TypeName} {f.PropertyName} => {f.FieldName};");
                    sb.AppendLine();
                    break;

                case FieldKind.Child:
                    sb.AppendLine($"    public {f.TypeName} {f.PropertyName}");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        get => {f.FieldName};");
                    sb.AppendLine("        set");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            if (object.ReferenceEquals({f.FieldName}, value)) return;");
                    sb.AppendLine($"            {track}.Untrack({f.FieldName}, {f.PropertyName}__OnChildChanged);");
                    sb.AppendLine($"            {f.FieldName} = value;");
                    sb.AppendLine($"            {track}.Track({f.FieldName}, {f.PropertyName}__OnChildChanged);");
                    sb.AppendLine($"            PropertyChanged?.Invoke(this, new {pcea}(nameof({f.PropertyName})));");
                    sb.AppendLine("        }");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                    sb.AppendLine($"    private void {f.PropertyName}__OnChildChanged(object? sender, {pcea} e)");
                    sb.AppendLine($"        => PropertyChanged?.Invoke(this, new {pcea}(nameof({f.PropertyName})));");
                    sb.AppendLine();
                    break;
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private enum FieldKind
    {
        Scalar,
        ScalarReadOnly,
        Collection,
        Child,
        ChildReadOnly
    }

    private sealed class Candidate(ClassDeclarationSyntax syntax, INamedTypeSymbol symbol)
    {
        public ClassDeclarationSyntax Syntax { get; } = syntax;
        public INamedTypeSymbol Symbol { get; } = symbol;
    }

    private sealed class FieldModel(string fieldName, string propertyName, string typeName, FieldKind kind)
    {
        public string FieldName { get; } = fieldName;
        public string PropertyName { get; } = propertyName;
        public string TypeName { get; } = typeName;
        public FieldKind Kind { get; } = kind;
    }
}
