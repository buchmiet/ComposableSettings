using System.Linq;
using Microsoft.CodeAnalysis;

namespace ComposableSettings.Generator.Helpers;

internal static class SettingsNameHelper
{
    public static bool IsValidPathSegment(string? value)
    {
        return value is not null
            && value.Length > 0
            && !string.IsNullOrWhiteSpace(value)
            && value.All(IsAllowedSegmentCharacter);
    }

    public static string ResolveComponentName(ITypeSymbol type)
    {
        var attribute = type.GetAttribute(GeneratorConstants.SettingsComponentAttributeFullName);
        if (attribute?.ConstructorArguments.Length > 0
            && attribute.ConstructorArguments[0].Value is string attributeName)
        {
            return attributeName;
        }

        var name = type.Name;
        return name.EndsWith(GeneratorConstants.ViewModelSuffix, System.StringComparison.Ordinal)
            ? name.Substring(0, name.Length - GeneratorConstants.ViewModelSuffix.Length)
            : name;
    }

    private static bool IsAllowedSegmentCharacter(char value)
    {
        return value is >= 'A' and <= 'Z'
            || value is >= 'a' and <= 'z'
            || value is >= '0' and <= '9'
            || value is '_' or '-' or '.';
    }
}
