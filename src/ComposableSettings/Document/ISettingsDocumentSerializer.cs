namespace ComposableSettings.Document;

/// <summary>Codec for whole-document settings files.</summary>
public interface ISettingsDocumentSerializer<TDocument>
    where TDocument : class, new()
{
    TDocument Deserialize(string? json, TDocument defaults);

    TDocument Deserialize(ReadOnlySpan<byte> utf8Json, TDocument defaults);

    string Serialize(TDocument value);

    byte[] SerializeUtf8(TDocument value);

    TDocument Clone(TDocument value);
}
