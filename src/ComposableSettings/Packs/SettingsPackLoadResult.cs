namespace ComposableSettings.Packs;

public sealed class SettingsPackLoadResult
{
    public required string RootDirectory { get; init; }

    public required SettingsPackManifest Manifest { get; init; }

    public string? OverlayJson { get; init; }
}
