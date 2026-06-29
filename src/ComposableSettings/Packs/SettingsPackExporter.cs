using System.Text.Json;
using ComposableSettings.Document;

namespace ComposableSettings.Packs;

public sealed class SettingsPackExporter<TDocument> : ISettingsPackExporter<TDocument>
    where TDocument : class, new()
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { WriteIndented = true };

    private readonly ISettingsDocumentSerializer<TDocument> _serializer;

    public SettingsPackExporter(ISettingsDocumentSerializer<TDocument>? serializer = null)
    {
        _serializer = serializer ?? new JsonSettingsDocumentSerializer<TDocument>();
    }

    public Task ExportAsync(
        string outputPath,
        TDocument overlay,
        SettingsPackManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        var staging = Path.Combine(Path.GetTempPath(), "ComposableSettings.PackExport", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            WritePackDirectory(staging, overlay, manifest);

            if (Directory.Exists(outputPath))
            {
                SettingsPackArchive.CopyDirectory(staging, outputPath);
            }
            else if (outputPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                     || outputPath.EndsWith(".settingspack", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                SettingsPackArchive.CreateZipFromDirectory(staging, outputPath);
            }
            else
            {
                Directory.CreateDirectory(outputPath);
                SettingsPackArchive.CopyDirectory(staging, outputPath);
            }
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }

        return Task.CompletedTask;
    }

    internal static void WritePackDirectory(string rootDirectory, TDocument overlay, SettingsPackManifest manifest)
    {
        manifest.SchemaVersion = SettingsPackManifest.SupportedSchemaVersion;
        var manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions);
        File.WriteAllText(Path.Combine(rootDirectory, "pack.json"), manifestJson);

        var overlayJson = JsonSerializer.Serialize(overlay, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });
        File.WriteAllText(Path.Combine(rootDirectory, "settings.overlay.json"), overlayJson);
    }
}
