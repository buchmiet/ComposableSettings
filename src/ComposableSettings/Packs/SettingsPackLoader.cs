using System.Text;
using System.Text.Json;
using ComposableSettings.Document;

namespace ComposableSettings.Packs;

public  class SettingsPackLoader : ISettingsPackLoader
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly SettingsPackOptions _options;

    public SettingsPackLoader(SettingsPackOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public ValueTask<SettingsPackLoadResult?> LoadAsync(string packPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(packPath))
            return ValueTask.FromResult<SettingsPackLoadResult?>(null);

        try
        {
            var cacheDirectory = Path.Combine(_options.CacheDirectory, GetPackCacheName(packPath));
            var stamp = SettingsPackArchive.ComputeSourceStamp(packPath);
            if (!SettingsPackArchive.IsCacheFresh(cacheDirectory, stamp))
            {
                if (Directory.Exists(cacheDirectory))
                    Directory.Delete(cacheDirectory, recursive: true);

                Directory.CreateDirectory(cacheDirectory);
                if (Directory.Exists(packPath))
                    SettingsPackArchive.CopyDirectory(packPath, cacheDirectory);
                else
                    SettingsPackArchive.ExtractZip(packPath, cacheDirectory);

                SettingsPackArchive.WriteStamp(cacheDirectory, stamp);
            }

            return ValueTask.FromResult(BuildLoadResult(cacheDirectory));
        }
        catch
        {
            return ValueTask.FromResult<SettingsPackLoadResult?>(null);
        }
    }

    internal SettingsPackLoadResult? BuildLoadResult(string rootDirectory)
    {
        var manifestPath = Path.Combine(rootDirectory, _options.ManifestFileName);
        if (!File.Exists(manifestPath))
            return null;

        SettingsPackManifest manifest;
        try
        {
            var utf8Json = Utf8SettingsFile.ReadAllBytes(manifestPath);
            manifest = JsonSerializer.Deserialize<SettingsPackManifest>(utf8Json, ManifestJsonOptions)
                       ?? throw new InvalidOperationException("Invalid pack manifest.");
        }
        catch (JsonException)
        {
            return null;
        }

        if (manifest.SchemaVersion != SettingsPackManifest.SupportedSchemaVersion)
            return null;

        var overlayPath = Path.Combine(rootDirectory, _options.OverlayFileName);
        if (!File.Exists(overlayPath))
            overlayPath = Path.Combine(rootDirectory, _options.LegacyOverlayFileName);

        string? overlayJson = File.Exists(overlayPath)
            ? Encoding.UTF8.GetString(Utf8SettingsFile.ReadAllBytes(overlayPath))
            : null;

        return new SettingsPackLoadResult
        {
            RootDirectory = rootDirectory,
            Manifest = manifest,
            OverlayJson = overlayJson,
        };
    }

    private static string GetPackCacheName(string packPath)
    {
        var fileName = Path.GetFileName(packPath);
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension))
            return Path.GetFileNameWithoutExtension(fileName);

        return fileName;
    }
}
