namespace ComposableSettings.Packs;

public sealed class SettingsPackInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string SourcePath { get; init; }
}
