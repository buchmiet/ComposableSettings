namespace ComposableSettings.Document;

public sealed class SettingsDocumentOptions<TDocument>
    where TDocument : class, new()
{
    public required string FilePath { get; init; }

    public required Func<TDocument> DefaultsFactory { get; init; }

    public TimeSpan AutosaveDelay { get; init; } = TimeSpan.FromMilliseconds(750);

    public bool UseAtomicWrites { get; init; } = true;

    public Action<TDocument>? Normalize { get; init; }

    public ISettingsDocumentSerializer<TDocument>? Serializer { get; init; }
}
