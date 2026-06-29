using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ComposableSettings.Generator.Helpers;

/// <summary>
/// Resolves dot-paths into document types for <see cref="SettingsDraftVmGenerator"/>.
/// </summary>
internal static class DocumentMemberPathResolver
{
    public static bool TryResolvePath(
        INamedTypeSymbol documentType,
        string memberPath,
        out ITypeSymbol leafType,
        out string draftAccessorSuffix)
    {
        leafType = null!;
        draftAccessorSuffix = string.Empty;

        if (string.IsNullOrWhiteSpace(memberPath))
            return false;

        var segments = memberPath.Split('.');
        if (segments.Length == 0)
            return false;

        var accessorParts = new List<string>(segments.Length);
        var current = documentType;

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i].Trim();
            if (segment.Length == 0)
                return false;

            if (!SettingsModelMemberResolver.TryResolveMemberType(current, segment, out var memberType))
                return false;

            accessorParts.Add(segment);

            if (i == segments.Length - 1)
            {
                leafType = memberType;
                draftAccessorSuffix = string.Join(".", accessorParts);
                return true;
            }

            if (memberType.IsValueType)
                return false;

            current = memberType as INamedTypeSymbol
                      ?? throw new InvalidOperationException("Expected named type for reference member.");
        }

        return false;
    }
}
