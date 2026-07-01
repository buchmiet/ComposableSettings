using ComposableSettings.Document;

namespace ComposableSettings.Packs;

public class SettingsPackCatalog<TDocument>(
    SettingsPackOptions options,
    ISettingsPackLoader loader,
    ISettingsDocumentSerializer<TDocument> serializer)
    : ISettingsPackCatalog<TDocument>
    where TDocument : class, new()
{
    private readonly Lock _gate = new();
    private readonly int _ = EnsureNotNull(options, loader, serializer);

    public event EventHandler? PackCacheChanged;

    public IReadOnlyList<SettingsPackInfo> ListInstalled()
    {
        if (string.IsNullOrWhiteSpace(options.PacksDirectory))
            return [];

        Directory.CreateDirectory(options.PacksDirectory);
        var results = new List<SettingsPackInfo>();

        foreach (var path in Directory.EnumerateFileSystemEntries(options.PacksDirectory))
        {
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
                continue;

            if (File.Exists(path) && !fileName.EndsWith(options.Extension, StringComparison.OrdinalIgnoreCase))
                continue;

            var load = loader.LoadAsync(path).GetAwaiter().GetResult();
            if (load is null)
                continue;

            var id = string.IsNullOrWhiteSpace(load.Manifest.Id)
                ? Path.GetFileNameWithoutExtension(fileName)
                : load.Manifest.Id;

            results.Add(new SettingsPackInfo
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(load.Manifest.Name) ? id : load.Manifest.Name,
                Version = load.Manifest.Version,
                SourcePath = path,
            });
        }

        return results;
    }

    public TDocument? TryLoadOverlay(string packId, TDocument defaults)
    {
        if (string.IsNullOrWhiteSpace(packId))
            return null;

        var packPath = ResolvePackPath(packId.Trim());
        if (packPath is null)
            return null;

        var load = loader.LoadAsync(packPath).GetAwaiter().GetResult();
        if (load?.OverlayJson is null)
            return null;

        return serializer.Deserialize(load.OverlayJson, defaults);
    }

    internal void InvalidateCache()
    {
        lock (_gate)
        {
            if (Directory.Exists(options.CacheDirectory))
                Directory.Delete(options.CacheDirectory, recursive: true);
        }

        PackCacheChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? ResolvePackPath(string packId)
    {
        if (string.IsNullOrWhiteSpace(options.PacksDirectory))
            return null;

        Directory.CreateDirectory(options.PacksDirectory);
        var direct = Path.Combine(options.PacksDirectory, packId);
        if (Directory.Exists(direct) || File.Exists(direct))
            return direct;

        if (!string.IsNullOrEmpty(options.Extension)
            && !packId.EndsWith(options.Extension, StringComparison.OrdinalIgnoreCase))
        {
            var withExtension = Path.Combine(options.PacksDirectory, packId + options.Extension);
            if (Directory.Exists(withExtension) || File.Exists(withExtension))
                return withExtension;
        }

        return null;
    }

    private static int EnsureNotNull(
        SettingsPackOptions options,
        ISettingsPackLoader loader,
        ISettingsDocumentSerializer<TDocument> serializer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(serializer);
        return 0;
    }
}
