namespace ComposableSettings.Packs;

public  class SettingsPackManifest
{
    public const int SupportedSchemaVersion = 1;

    public int SchemaVersion { get; set; } = SupportedSchemaVersion;

    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Version { get; set; } = "1.0.0";

    public string? Author { get; set; }
}
