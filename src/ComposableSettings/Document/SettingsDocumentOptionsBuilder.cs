namespace ComposableSettings.Document;

public  class SettingsDocumentOptionsBuilder<TDocument>
    where TDocument : class, new()
{
    public string FilePath { get; set; } = string.Empty;

    public Func<TDocument> DefaultsFactory { get; set; } = () => new TDocument();

    public TimeSpan AutosaveDelay { get; set; } = TimeSpan.FromMilliseconds(750);

    public bool UseAtomicWrites { get; set; } = true;

    public Action<TDocument>? Normalize { get; set; }

    public ISettingsDocumentSerializer<TDocument>? Serializer { get; set; }

    internal SettingsDocumentOptions<TDocument> Build()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
            throw new InvalidOperationException($"{nameof(FilePath)} must be set.");

        return new SettingsDocumentOptions<TDocument>
        {
            FilePath = FilePath,
            DefaultsFactory = DefaultsFactory,
            AutosaveDelay = AutosaveDelay,
            UseAtomicWrites = UseAtomicWrites,
            Normalize = Normalize,
            Serializer = Serializer,
        };
    }
}
