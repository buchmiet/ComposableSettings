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

    public Task<SettingsPackLoadResult?> LoadAsync(string packPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(packPath))
            return Task.FromResult<SettingsPackLoadResult?>(null);

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

            return Task.FromResult(BuildLoadResult(cacheDirectory));
        }
        catch
        {
            return Task.FromResult<SettingsPackLoadResult?>(null);
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
            var json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<SettingsPackManifest>(json, ManifestJsonOptions)
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

        string? overlayJson = File.Exists(overlayPath) ? File.ReadAllText(overlayPath) : null;

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
