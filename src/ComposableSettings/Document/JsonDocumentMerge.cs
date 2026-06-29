using System.Text.Json;
using System.Text.Json.Nodes;

namespace ComposableSettings.Document;

/// <summary>JSON deep-merge helpers for document layering (D1: defaults + user).</summary>
internal static class JsonDocumentMerge
{
    private static readonly JsonSerializerOptions MergeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static TDocument MergeOverlay<TDocument>(TDocument baseline, TDocument overlay)
        where TDocument : class, new()
        => MergeOverlay(baseline, overlay, new TDocument());

    public static TDocument MergeOverlay<TDocument>(TDocument baseline, TDocument overlay, TDocument overlayDefaults)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(overlayDefaults);

        var baselineNode = JsonSerializer.SerializeToNode(baseline, MergeOptions) as JsonObject ?? new JsonObject();
        var overlayNode = JsonSerializer.SerializeToNode(overlay, MergeOptions) as JsonObject ?? new JsonObject();
        var overlayDefaultNode = JsonSerializer.SerializeToNode(overlayDefaults, MergeOptions) as JsonObject ?? new JsonObject();
        DeepMergeNonDefault(baselineNode, overlayNode, overlayDefaultNode);
        return JsonSerializer.Deserialize<TDocument>(baselineNode.ToJsonString(), MergeOptions) ?? new TDocument();
    }

    private static void DeepMergeNonDefault(JsonObject target, JsonObject overlay, JsonObject overlayDefaults)
    {
        foreach (var property in overlay)
        {
            overlayDefaults.TryGetPropertyValue(property.Key, out var defaultValue);
            if (JsonNode.DeepEquals(property.Value, defaultValue))
                continue;

            if (property.Value is JsonObject overlayObject
                && target[property.Key] is JsonObject targetObject
                && defaultValue is JsonObject defaultObject)
            {
                DeepMergeNonDefault(targetObject, overlayObject, defaultObject);
            }
            else
            {
                target[property.Key] = property.Value?.DeepClone();
            }
        }
    }
}
