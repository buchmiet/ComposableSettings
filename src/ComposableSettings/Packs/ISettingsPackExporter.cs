namespace ComposableSettings.Packs;

public interface ISettingsPackExporter<TDocument>
    where TDocument : class, new()
{
    Task ExportAsync(
        string outputPath,
        TDocument overlay,
        SettingsPackManifest manifest,
        CancellationToken cancellationToken = default);
}
