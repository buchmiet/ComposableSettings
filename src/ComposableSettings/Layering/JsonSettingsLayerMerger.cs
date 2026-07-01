using System.Text.Json;
using System.Text.Json.Nodes;
using ComposableSettings.Document;

namespace ComposableSettings.Layering;

public class JsonSettingsLayerMerger<TDocument>(
    SettingsMergePolicy policy,
    ISettingsDocumentSerializer<TDocument> serializer)
    : ISettingsLayerMerger<TDocument>
    where TDocument : class, new()
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly int _ = EnsureNotNull(policy, serializer);

    public TDocument Merge(TDocument defaults, TDocument? packOverlay, TDocument userLayer)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(userLayer);

        var resultNode = ToNode(defaults);
        var emptyNode = ToNode(new TDocument());

        if (packOverlay is not null)
            ApplyLayer(resultNode, ToNode(packOverlay), emptyNode, isPackLayer: true);

        ApplyLayer(resultNode, ToNode(userLayer), emptyNode, isPackLayer: false);
        ApplyUserOwnedRootProperties(resultNode, ToNode(userLayer));

        return Deserialize(resultNode);
    }

    private void ApplyLayer(JsonObject target, JsonObject overlay, JsonObject overlayDefaults, bool isPackLayer)
    {
        var mergeMode = isPackLayer ? policy.PackMergeMode : policy.UserMergeMode;
        var properties = ResolveMergeableProperties(isPackLayer);

        if (properties.Count == 0)
        {
            MergeNodes(target, overlay, overlayDefaults, mergeMode);
            return;
        }

        foreach (var propertyName in properties)
        {
            if (!overlay.TryGetPropertyValue(propertyName, out var overlayValue) || overlayValue is null)
                continue;

            overlayDefaults.TryGetPropertyValue(propertyName, out var defaultValue);
            if (mergeMode == SettingsMergeMode.DeepMergeNonDefault
                && JsonNode.DeepEquals(overlayValue, defaultValue))
            {
                continue;
            }

            if (overlayValue is JsonObject overlayObject
                && target[propertyName] is JsonObject targetObject
                && defaultValue is JsonObject defaultObject
                && mergeMode != SettingsMergeMode.Replace)
            {
                MergeNodes(targetObject, overlayObject, defaultObject, mergeMode);
            }
            else
            {
                target[propertyName] = overlayValue.DeepClone();
            }
        }
    }

    private HashSet<string> ResolveMergeableProperties(bool isPackLayer)
    {
        if (policy.MergeableRootProperties.Count == 0)
            return [];

        var set = new HashSet<string>(policy.MergeableRootProperties, StringComparer.OrdinalIgnoreCase);
        if (isPackLayer)
        {
            set.ExceptWith(policy.ExcludedFromPackMerge);
        }

        return set;
    }

    private void ApplyUserOwnedRootProperties(JsonObject target, JsonObject userNode)
    {
        foreach (var propertyName in policy.UserOwnedRootProperties)
        {
            if (userNode.TryGetPropertyValue(propertyName, out var value))
                target[propertyName] = value?.DeepClone();
        }
    }

    private static void MergeNodes(
        JsonObject target,
        JsonObject overlay,
        JsonObject overlayDefaults,
        SettingsMergeMode mergeMode)
    {
        switch (mergeMode)
        {
            case SettingsMergeMode.Replace:
                foreach (var property in overlay)
                    target[property.Key] = property.Value?.DeepClone();
                break;
            case SettingsMergeMode.DeepMerge:
                DeepMerge(target, overlay);
                break;
            case SettingsMergeMode.DeepMergeNonDefault:
                DeepMergeNonDefault(target, overlay, overlayDefaults);
                break;
        }
    }

    private static void DeepMerge(JsonObject target, JsonObject overlay)
    {
        foreach (var property in overlay)
        {
            if (property.Value is JsonObject overlayObject
                && target[property.Key] is JsonObject targetObject)
            {
                DeepMerge(targetObject, overlayObject);
            }
            else
            {
                target[property.Key] = property.Value?.DeepClone();
            }
        }
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

    private JsonObject ToNode(TDocument document) =>
        JsonSerializer.SerializeToNode(document, JsonOptions) as JsonObject ?? new JsonObject();

    private TDocument Deserialize(JsonObject node) =>
        node.Deserialize<TDocument>(JsonOptions) ?? new TDocument();

    private static int EnsureNotNull(
        SettingsMergePolicy policy,
        ISettingsDocumentSerializer<TDocument> serializer)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(serializer);
        return 0;
    }
}
