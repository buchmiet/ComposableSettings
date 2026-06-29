namespace ComposableSettings.Document;

/// <summary>Codec for whole-document settings files.</summary>
public interface ISettingsDocumentSerializer<TDocument>
    where TDocument : class, new()
{
    TDocument Deserialize(string? json, TDocument defaults);

    string Serialize(TDocument value);

    TDocument Clone(TDocument value);
}
