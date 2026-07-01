namespace ComposableSettings.Packs;

public  class SettingsPackLoadResult
{
    public required string RootDirectory { get; init; }

    public required SettingsPackManifest Manifest { get; init; }

    public string? OverlayJson { get; init; }
}
