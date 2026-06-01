using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ComposableSettings.Generator.Helpers;

/// <summary>
/// Resolves settings members the same way <see cref="SourceGenerators.SettingsModelGenerator"/>
/// would expose them, so other generators can validate against underscore fields before
/// the model source is emitted.
/// </summary>
internal static class SettingsModelMemberResolver
{
    private const string ObservableCollectionDefinition = "System.Collections.ObjectModel.ObservableCollection<T>";

    public static string ToPropertyName(string fieldName)
    {
        var trimmed = fieldName.TrimStart('_');
        return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
    }

    public static bool TryResolveMemberType(
        INamedTypeSymbol settingsType,
        string propertyName,
        out ITypeSymbol memberType)
    {
        memberType = null!;

        var settingsProperty = settingsType.GetMembers(propertyName)
            .OfType<IPropertySymbol>()
            .FirstOrDefault(p => p.DeclaredAccessibility == Accessibility.Public);
        if (settingsProperty is not null)
        {
            memberType = settingsProperty.Type;
            return true;
        }

        foreach (var field in settingsType.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsImplicitlyDeclared || field.IsStatic || field.IsConst || field.AssociatedSymbol is not null)
                continue;
            if (!field.Name.StartsWith("_", StringComparison.Ordinal) || field.Name.TrimStart('_').Length == 0)
                continue;
            if (ToPropertyName(field.Name) != propertyName)
                continue;
            if (settingsType.GetMembers(propertyName).OfType<IPropertySymbol>().Any())
                continue;
            if (!WouldGenerateProperty(field))
                continue;

            memberType = field.Type;
            return true;
        }

        return false;
    }

    private static bool WouldGenerateProperty(IFieldSymbol field)
    {
        if (IsObservableCollection(field.Type))
            return true;

        if (field.Type.SpecialType == SpecialType.System_String || field.Type.IsValueType)
            return !field.IsReadOnly;

        return !field.IsReadOnly;
    }

    private static bool IsObservableCollection(ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true } named
           && named.ConstructedFrom.ToDisplayString() == ObservableCollectionDefinition;
}
