namespace ComposableSettings.Layering;

/// <summary>Declarative rules for defaults → pack → user document merge.</summary>
public sealed class SettingsMergePolicy
{
    /// <summary>Root JSON properties (camelCase) merged from pack and user overlays.</summary>
    public HashSet<string> MergeableRootProperties { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Root properties ignored when applying the pack layer.</summary>
    public HashSet<string> ExcludedFromPackMerge { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Root properties always taken from the user layer (replace), not pack.</summary>
    public HashSet<string> UserOwnedRootProperties { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SettingsMergeMode PackMergeMode { get; set; } = SettingsMergeMode.DeepMerge;

    public SettingsMergeMode UserMergeMode { get; set; } = SettingsMergeMode.DeepMergeNonDefault;
}
