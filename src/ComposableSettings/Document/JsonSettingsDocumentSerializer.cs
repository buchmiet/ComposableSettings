using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComposableSettings.Document;

/// <summary>System.Text.Json implementation with tolerant deserialize.</summary>
public class JsonSettingsDocumentSerializer<TDocument> : ISettingsDocumentSerializer<TDocument>
    where TDocument : class, new()
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public TDocument Deserialize(string? json, TDocument defaults)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Clone(defaults);

        try
        {
            return JsonSerializer.Deserialize<TDocument>(json, SerializerOptions) ?? Clone(defaults);
        }
        catch
        {
            return Clone(defaults);
        }
    }

    public TDocument Deserialize(ReadOnlySpan<byte> utf8Json, TDocument defaults)
    {
        if (utf8Json.IsEmpty || IsWhiteSpaceUtf8(utf8Json))
            return Clone(defaults);

        try
        {
            return JsonSerializer.Deserialize<TDocument>(utf8Json, SerializerOptions) ?? Clone(defaults);
        }
        catch
        {
            return Clone(defaults);
        }
    }

    public string Serialize(TDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    public byte[] SerializeUtf8(TDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
    }

    public TDocument Clone(TDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Deserialize(SerializeUtf8(value), new TDocument());
    }

    private static bool IsWhiteSpaceUtf8(ReadOnlySpan<byte> utf8Json)
    {
        foreach (var b in utf8Json)
        {
            if (b is not ((byte)'\t' or (byte)'\n' or (byte)'\r' or (byte)' '))
                return false;
        }

        return true;
    }
}
