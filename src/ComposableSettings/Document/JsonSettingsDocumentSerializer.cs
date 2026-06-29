using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComposableSettings.Document;

/// <summary>System.Text.Json implementation with tolerant deserialize.</summary>
public sealed class JsonSettingsDocumentSerializer<TDocument> : ISettingsDocumentSerializer<TDocument>
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

    public string Serialize(TDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    public TDocument Clone(TDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Deserialize(Serialize(value), new TDocument());
    }
}
