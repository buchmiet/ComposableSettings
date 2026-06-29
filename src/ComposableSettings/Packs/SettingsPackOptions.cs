namespace ComposableSettings.Packs;

public sealed class SettingsPackOptions
{
    public string PacksDirectory { get; set; } = "";

    public string CacheDirectory { get; set; } = "";

    /// <summary>Pack archive extension when resolving by id (e.g. <c>.settingspack</c>).</summary>
    public string Extension { get; set; } = ".settingspack";

    public string ManifestFileName { get; set; } = "pack.json";

    public string OverlayFileName { get; set; } = "settings.overlay.json";

    /// <summary>Fallback overlay file when <see cref="OverlayFileName"/> is missing.</summary>
    public string LegacyOverlayFileName { get; set; } = "settings.json";
}
