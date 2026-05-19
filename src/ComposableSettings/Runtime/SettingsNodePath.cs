namespace ComposableSettings;

public sealed record SettingsNodePath
{
    private readonly string[] _segments;

    private SettingsNodePath(IEnumerable<string> segments)
    {
        _segments = segments.ToArray();
        Segments = Array.AsReadOnly(_segments);
    }

    public IReadOnlyList<string> Segments { get; }

    public static SettingsNodePath Root(string segment)
    {
        return new SettingsNodePath([ValidateSegment(segment)]);
    }

    public SettingsNodePath Child(string segment)
    {
        return new SettingsNodePath(_segments.Append(ValidateSegment(segment)));
    }

    public override string ToString() => string.Join("/", _segments);

    internal static string ValidateSegment(string? segment)
    {
        if (segment is null)
            throw new ArgumentException("Settings path segment cannot be null.", nameof(segment));
        if (segment.Length == 0)
            throw new ArgumentException("Settings path segment cannot be empty.", nameof(segment));
        if (string.IsNullOrWhiteSpace(segment))
            throw new ArgumentException("Settings path segment cannot contain only whitespace.", nameof(segment));
        if (segment.Contains('/'))
            throw new ArgumentException($"Settings path segment '{segment}' cannot contain '/'.", nameof(segment));
        return !segment.All(IsAllowedSegmentCharacter) ? throw new ArgumentException($"Settings path segment '{segment}' can contain only letters, digits, '_', '-' or '.'.", nameof(segment)) : segment;
    }

    private static bool IsAllowedSegmentCharacter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
               || value is >= '0' and <= '9'
               || value is '_' or '-' or '.';
    }
}
