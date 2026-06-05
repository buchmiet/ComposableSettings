using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ComposableSettings.Configuration;
using ComposableSettings.Runtime;
using ComposableSettings.Static;

namespace ComposableSettings.Stores;

/// <summary>
/// JSON settings file backed by <see cref="System.Text.Json"/> (full-rewrite per <c>Set</c>).
/// Node paths address keys in the root object, mirroring <see cref="XmlSettingsFile"/> segment semantics.
/// </summary>
public class JsonSettingsFile : IComponentSettingsProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly object _gate = new();
    private JsonObject _document;

    public JsonSettingsFile(SettingsFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        SettingsFilePath = SettingsPathResolver.ResolveJsonFilePath(options);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        _document = LoadOrCreateDocument(SettingsFilePath);
    }

    /// <summary>
    /// Creates an instance from a fully-resolved file path instead of option-based resolution.
    /// The directory is created if it does not exist.
    /// </summary>
    public JsonSettingsFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        SettingsFilePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        _document = LoadOrCreateDocument(SettingsFilePath);
    }

    public string SettingsFilePath { get; }

    public TSettings Get<TSettings>(SettingsNodePath path)
        where TSettings : class, new()
    {
        ArgumentNullException.ThrowIfNull(path);

        lock (_gate)
        {
            var node = FindNode(_document, path.Segments);
            if (node is null)
                return new TSettings();

            try
            {
                return JsonSerializer.Deserialize<TSettings>(node, SerializerOptions) ?? new TSettings();
            }
            catch
            {
                return new TSettings();
            }
        }
    }

    public void Set<TSettings>(SettingsNodePath path, TSettings value)
        where TSettings : class, new()
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            Write(_document, path, value);
            Flush();
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
        => new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = null,
            Converters = { new JsonStringEnumConverter() }
        };

    private static JsonObject LoadOrCreateDocument(string filePath)
    {
        if (!File.Exists(filePath))
            return new JsonObject();

        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new JsonObject();

            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static JsonNode? FindNode(JsonObject root, IReadOnlyList<string> segments)
    {
        JsonNode? current = root;
        foreach (var segment in segments)
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment, out current))
                return null;
        }

        return current;
    }

    private static void Write<TSettings>(JsonObject root, SettingsNodePath path, TSettings value)
        where TSettings : class, new()
    {
        var segments = path.Segments;
        var current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            if (!current.TryGetPropertyValue(segment, out var next) || next is not JsonObject nextObject)
            {
                nextObject = new JsonObject();
                current[segment] = nextObject;
            }

            current = nextObject;
        }

        current[segments[^1]] = JsonSerializer.SerializeToNode(value, SerializerOptions);
    }

    private void Flush()
    {
        File.WriteAllText(SettingsFilePath, _document.ToJsonString(SerializerOptions));
    }
}
