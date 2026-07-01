using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace ComposableSettings.Generator.Helpers;

/// <summary>
/// Provides centralized, testable operations for type system manipulation in code generation.
/// </summary>
internal  class TypeSystemHelper
{
    private const string GlobalNamespacePrefix = "global::";

    private readonly HashSet<string> _csharpKeywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
        "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
        "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while", "value", "async", "await",
        "dynamic", "partial", "yield", "var", "when", "where", "add", "remove", "get", "set", "global",
        "alias", "ascending", "descending", "from", "group", "into", "join", "let", "orderby",
        "select", "where"
    ];

    private readonly Dictionary<string, string> _typeAliases = new()
    {
        { "System.String", "string" },
        { "System.Int32", "int" },
        { "System.Int64", "long" },
        { "System.Boolean", "bool" },
        { "System.Byte", "byte" },
        { "System.Char", "char" },
        { "System.Decimal", "decimal" },
        { "System.Double", "double" },
        { "System.Single", "float" },
        { "System.Object", "object" },
        { "System.Void", "void" },
        { "System.UInt32", "uint" },
        { "System.UInt64", "ulong" },
        { "System.Int16", "short" },
        { "System.UInt16", "ushort" },
        { "System.SByte", "sbyte" }
    };

    private static readonly Regex ArraySuffixRegex =
        new(@"(\[\s*(,\s*)*\])+$", RegexOptions.Compiled);

    /// <summary>
    /// Formatuje w pełni kwalifikowaną nazwę typu do użycia w wygenerowanym kodzie.
    /// </summary>
    public string FormatTypeForUsage(string fullyQualifiedTypeName, bool useGlobalPrefix = false)
    {
        if (string.IsNullOrEmpty(fullyQualifiedTypeName))
            return "object";

        Match arrayMatch = ArraySuffixRegex.Match(fullyQualifiedTypeName);
        if (arrayMatch.Success)
        {
            string elementType = fullyQualifiedTypeName.Substring(0, arrayMatch.Index);
            string suffix = arrayMatch.Value;
            return FormatTypeForUsage(elementType, useGlobalPrefix) + suffix;
        }

        if (fullyQualifiedTypeName.Length > 1 &&
            fullyQualifiedTypeName[fullyQualifiedTypeName.Length - 1] == '?')
        {
            string elementType = fullyQualifiedTypeName.Substring(0, fullyQualifiedTypeName.Length - 1);
            return FormatTypeForUsage(elementType, useGlobalPrefix) + "?";
        }

        if (IsGenericType(fullyQualifiedTypeName))
            return FormatGenericType(fullyQualifiedTypeName, useGlobalPrefix);

        string typeName = fullyQualifiedTypeName.Replace("+", ".");

        if (typeName.StartsWith(GlobalNamespacePrefix, StringComparison.Ordinal))
            typeName = typeName.Substring(GlobalNamespacePrefix.Length);

        if (_typeAliases.TryGetValue(typeName, out var alias))
            return alias;

        if (IsNestedType(fullyQualifiedTypeName))
            return useGlobalPrefix ? GlobalNamespacePrefix + typeName : typeName;

        string simpleName = GetSimpleTypeName(typeName);
        return useGlobalPrefix ? GlobalNamespacePrefix + typeName : simpleName;
    }

    public string? GetNamespace(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedTypeName))
            return null;

        var typeName = RemoveGlobalPrefix(fullyQualifiedTypeName);

        if (IsGenericType(typeName))
        {
            var genericStart = typeName.IndexOfAny(['<', '`']);
            if (genericStart > 0)
                typeName = typeName.Substring(0, genericStart);
        }

        if (typeName.Contains('+'))
        {
            var plusIndex = typeName.IndexOf('+');
            typeName = typeName.Substring(0, plusIndex);
        }

        var lastDot = typeName.LastIndexOf('.');
        return lastDot > 0 ? typeName.Substring(0, lastDot) : null;
    }

    public string GetSimpleTypeName(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedTypeName))
            return "object";

        string typeName = RemoveGlobalPrefix(fullyQualifiedTypeName);

        int genericMarker = typeName.IndexOfAny(new[] { '<', '`' });
        if (genericMarker > 0)
            typeName = typeName.Substring(0, genericMarker);

        int plusFirst = typeName.IndexOf('+');
        if (plusFirst >= 0)
        {
            int plusCount = 1;
            for (int i = plusFirst + 1; i < typeName.Length; i++)
                if (typeName[i] == '+') plusCount++;

            int lastDotBeforePlus = typeName.LastIndexOf('.', plusFirst);
            int start;

            if (plusCount == 1)
                start = 0;
            else if (lastDotBeforePlus >= 0)
            {
                int prevDot = typeName.LastIndexOf('.', lastDotBeforePlus - 1);
                start = prevDot >= 0 ? prevDot + 1 : lastDotBeforePlus + 1;
            }
            else
                start = 0;

            return typeName.Substring(start).Replace('+', '.');
        }

        int lastDot = typeName.LastIndexOf('.');
        return lastDot >= 0 ? typeName.Substring(lastDot + 1) : typeName;
    }

    public bool IsNestedType(string fullyQualifiedTypeName)
        => !string.IsNullOrEmpty(fullyQualifiedTypeName) && fullyQualifiedTypeName.Contains('+');

    public bool IsGenericType(string typeName)
        => !string.IsNullOrEmpty(typeName) && (typeName.Contains('`') || typeName.Contains('<'));

    public string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return identifier;

        return _csharpKeywords.Contains(identifier) ? $"@{identifier}" : identifier;
    }

    public string BuildFullTypeName(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol is null)
            throw new ArgumentNullException(nameof(typeSymbol));

        if (typeSymbol.IsGenericType)
            return BuildGenericTypeName(typeSymbol);

        var parts = new List<string> { typeSymbol.Name };
        var current = typeSymbol.ContainingType;
        while (current is not null)
        {
            parts.Insert(0, current.Name);
            current = current.ContainingType;
        }

        var typePath = string.Join("+", parts);
        var ns = typeSymbol.ContainingNamespace;
        if (ns is { IsGlobalNamespace: false })
            return $"{ns.ToDisplayString()}.{typePath}";

        return typePath;
    }

    public IEnumerable<string> GetRequiredNamespaces(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedTypeName))
            yield break;

        if (IsNestedType(fullyQualifiedTypeName) && !IsGenericType(fullyQualifiedTypeName))
            yield break;

        var ns = GetNamespace(fullyQualifiedTypeName);
        if (!string.IsNullOrEmpty(ns))
            yield return ns;

        if (IsGenericType(fullyQualifiedTypeName))
        {
            foreach (var argNs in ExtractGenericArgumentNamespaces(fullyQualifiedTypeName))
            {
                if (!string.IsNullOrEmpty(argNs))
                    yield return argNs;
            }
        }
    }

    public string FormatForTypeof(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedTypeName))
            return "object";

        string raw = RemoveGlobalPrefix(fullyQualifiedTypeName);

        if (IsGenericType(raw) && raw.IndexOf('`') >= 0)
        {
            var backtick = raw.IndexOf('`');
            var baseName = raw.Substring(0, backtick);
            int argCount = ExtractGenericArgumentCount(raw);
            var commas = new string(',', Math.Max(argCount - 1, 0));
            return $"{baseName}<{commas}>";
        }

        int open = raw.IndexOf('<');
        if (open >= 0)
        {
            int close = raw.LastIndexOf('>');
            if (close > open + 1)
            {
                string baseName = raw.Substring(0, open);
                string argsSection = raw.Substring(open + 1, close - open - 1);

                IEnumerable<string> argList;
                try
                {
                    argList = SplitGenericArguments(argsSection);
                }
                catch
                {
                    return raw;
                }

                string[] formattedArgs = argList
                    .Select(a => FormatTypeForUsage(a.Trim(), false))
                    .ToArray();

                string baseNorm = baseName.Replace('+', '.');
                bool needsGlobal = baseName.IndexOf('.') >= 0 || baseName.IndexOf('+') >= 0;
                string result = baseNorm + "<" + string.Join(", ", formattedArgs) + ">";
                return needsGlobal ? GlobalNamespacePrefix + result : result;
            }
        }

        bool needsGlobalSimple = raw.IndexOf('.') >= 0 || raw.IndexOf('+') >= 0;
        string simple = raw.Replace('+', '.');
        return needsGlobalSimple ? GlobalNamespacePrefix + simple : simple;
    }

    private string RemoveGlobalPrefix(string typeName)
    {
        if (typeName.StartsWith(GlobalNamespacePrefix, StringComparison.Ordinal))
            return typeName.Substring(GlobalNamespacePrefix.Length);
        return typeName;
    }

    private string FormatGenericType(string genericTypeName, bool useGlobalPrefix)
    {
        if (genericTypeName.Contains('`'))
            return ConvertClrGenericToFriendly(genericTypeName, useGlobalPrefix);

        return ProcessFriendlyGenericType(genericTypeName, useGlobalPrefix);
    }

    private string ConvertClrGenericToFriendly(string clrTypeName, bool useGlobalPrefix)
    {
        if (string.IsNullOrEmpty(clrTypeName))
            return "object";

        int backtick = clrTypeName.IndexOf('`');
        if (backtick < 0)
            return FormatTypeForUsage(clrTypeName, useGlobalPrefix);

        string baseRaw = clrTypeName.Substring(0, backtick);
        string baseSimple = GetSimpleTypeName(baseRaw);
        string baseNoGlobal = RemoveGlobalPrefix(baseRaw);
        string baseWithPrefix = useGlobalPrefix && !_typeAliases.ContainsValue(baseSimple)
            ? GlobalNamespacePrefix + baseNoGlobal
            : baseSimple;

        int firstBracket = clrTypeName.IndexOf('[', backtick);
        if (firstBracket < 0)
        {
            int argCount = ExtractGenericArgumentCount(clrTypeName);
            string commas = new string(',', Math.Max(argCount - 1, 0));
            return $"{baseWithPrefix}<{commas}>";
        }

        string argsSegment = clrTypeName.Substring(firstBracket);
        List<string> argSpecs = ExtractClrGenericArguments(argsSegment);
        string[] formattedArgs = argSpecs
            .Select(spec => FormatTypeForUsage(RemoveAssemblyQualifier(spec), useGlobalPrefix))
            .ToArray();

        return $"{baseWithPrefix}<{string.Join(", ", formattedArgs)}>";
    }

    private static string RemoveAssemblyQualifier(string typeSpec)
    {
        int depth = 0;
        for (int i = 0; i < typeSpec.Length; i++)
        {
            char c = typeSpec[i];
            switch (c)
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return typeSpec.Substring(0, i).Trim();
            }
        }

        return typeSpec.Trim();
    }

    private static List<string> ExtractClrGenericArguments(string segment)
    {
        var result = new List<string>();
        int depth = 0;
        int argStart = -1;

        for (int i = 0; i < segment.Length; i++)
        {
            char c = segment[i];
            switch (c)
            {
                case '[':
                    depth++;
                    if (depth == 2)
                        argStart = i + 1;
                    break;
                case ']':
                    if (depth == 2 && argStart >= 0)
                    {
                        result.Add(segment.Substring(argStart, i - argStart));
                        argStart = -1;
                    }

                    depth--;
                    break;
            }
        }

        return result;
    }

    private string ProcessFriendlyGenericType(string friendlyTypeName, bool useGlobalPrefix)
    {
        if (string.IsNullOrEmpty(friendlyTypeName))
            return "object";

        int open = friendlyTypeName.IndexOf('<');
        if (open < 0)
            return FormatTypeForUsage(friendlyTypeName, useGlobalPrefix);

        int close = friendlyTypeName.LastIndexOf('>');
        if (close < 0 || close <= open + 1)
            return friendlyTypeName;

        string baseRaw = friendlyTypeName.Substring(0, open);
        string argsSection = friendlyTypeName.Substring(open + 1, close - open - 1);

        IEnumerable<string> argList;
        try
        {
            argList = SplitGenericArguments(argsSection);
        }
        catch
        {
            return friendlyTypeName;
        }

        string[] formattedArgs = argList
            .Select(a => FormatTypeForUsage(a.Trim(), useGlobalPrefix))
            .ToArray();

        string baseNoGlobal = RemoveGlobalPrefix(baseRaw);
        string baseWithPrefix = useGlobalPrefix
            ? GlobalNamespacePrefix + baseNoGlobal.Replace('+', '.')
            : baseNoGlobal.IndexOf('+') >= 0
                ? baseNoGlobal.Replace('+', '.')
                : GetSimpleTypeName(baseNoGlobal);

        return baseWithPrefix + "<" + string.Join(", ", formattedArgs) + ">";
    }

    private static IEnumerable<string> SplitGenericArguments(string args)
    {
        var depth = 0;
        var sb = new StringBuilder();

        foreach (var ch in args)
        {
            if (ch == ',' && depth == 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
            else
            {
                switch (ch)
                {
                    case '<':
                        depth++;
                        break;
                    case '>':
                        depth--;
                        break;
                }

                sb.Append(ch);
            }
        }

        if (sb.Length > 0)
            yield return sb.ToString();
    }

    private static int ExtractGenericArgumentCount(string genericTypeName)
    {
        var backtickIndex = genericTypeName.IndexOf('`');
        if (backtickIndex < 0)
            return 0;

        var afterBacktick = genericTypeName.Substring(backtickIndex + 1);
        var digits = afterBacktick.TakeWhile(char.IsDigit).ToArray();
        return int.TryParse(new string(digits), out var count) ? count : 0;
    }

    private IEnumerable<string> ExtractGenericArgumentNamespaces(string genericTypeName)
    {
        if (genericTypeName.Contains('<') && genericTypeName.Contains('>'))
        {
            var start = genericTypeName.IndexOf('<');
            var end = genericTypeName.LastIndexOf('>');
            if (start < end)
            {
                foreach (var arg in genericTypeName.Substring(start + 1, end - start - 1).Split(','))
                {
                    var ns = GetNamespace(arg.Trim());
                    if (!string.IsNullOrEmpty(ns))
                        yield return ns;
                }
            }
        }
        else if (genericTypeName.IndexOf('`') >= 0 && genericTypeName.Contains("[["))
        {
            var backtick = genericTypeName.IndexOf('`');
            var firstBracket = genericTypeName.IndexOf('[', backtick);
            if (firstBracket >= 0)
            {
                foreach (var spec in ExtractClrGenericArguments(genericTypeName.Substring(firstBracket)))
                {
                    var ns = GetNamespace(RemoveAssemblyQualifier(spec));
                    if (!string.IsNullOrEmpty(ns))
                        yield return ns;
                }
            }
        }
    }

    private string BuildGenericTypeName(INamedTypeSymbol typeSymbol)
    {
        var definition = typeSymbol.OriginalDefinition;
        var parts = new List<string> { definition.Name };
        var current = definition.ContainingType;

        while (current is not null)
        {
            parts.Insert(0, current.Name);
            current = current.ContainingType;
        }

        var basePath = string.Join("+", parts);
        var ns = definition.ContainingNamespace;
        if (ns is { IsGlobalNamespace: false })
            basePath = ns.ToDisplayString() + "." + basePath;

        var backtick = basePath.IndexOf('`');
        if (backtick >= 0)
            basePath = basePath.Substring(0, backtick);

        var argNames = typeSymbol.TypeArguments
            .Select(arg => arg is INamedTypeSymbol nts
                ? BuildFullTypeName(nts)
                : arg.ToDisplayString())
            .ToArray();

        return basePath + "<" + string.Join(", ", argNames) + ">";
    }
}
