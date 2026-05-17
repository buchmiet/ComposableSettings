using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComposableSettings.Generator.Helpers;

internal static class SymbolExtensions
{
    public static bool HasAttribute(this ISymbol symbol, string attributeFullName)
    {
        return symbol.GetAttributes().Any(attribute => IsAttribute(attribute, attributeFullName));
    }

    public static AttributeData? GetAttribute(this ISymbol symbol, string attributeFullName)
    {
        return symbol.GetAttributes().FirstOrDefault(attribute => IsAttribute(attribute, attributeFullName));
    }

    public static string ToGlobalTypeName(this ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    public static string GetNamespaceName(this INamedTypeSymbol type)
    {
        return type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : string.Empty;
    }

    public static string GetAccessibilityText(this INamedTypeSymbol type)
    {
        return type.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => "internal"
        };
    }

    public static string GetTypeModifiersText(this INamedTypeSymbol type)
    {
        var modifiers = new List<string> { type.GetAccessibilityText() };

        if (type.IsStatic)
        {
            modifiers.Add("static");
        }
        else
        {
            if (type.IsSealed) modifiers.Add("sealed");
            if (type.IsAbstract) modifiers.Add("abstract");
        }

        modifiers.Add("partial");
        modifiers.Add(TypeKindText(type));

        return string.Join(" ", modifiers);
    }

    public static bool IsPartial(this INamedTypeSymbol type)
    {
        return type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(declaration => declaration.Modifiers.Any(modifier => modifier.ValueText == "partial"));
    }

    private static bool IsAttribute(AttributeData attribute, string attributeFullName)
    {
        return attribute.AttributeClass?.ToDisplayString() == attributeFullName;
    }

    private static string TypeKindText(INamedTypeSymbol type)
    {
        return type.TypeKind switch
        {
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            _ => "class"
        };
    }
}
