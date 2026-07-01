using ComposableSettings.Document;

namespace ComposableSettings.Packs;

public  class SettingsPackCatalog<TDocument> : ISettingsPackCatalog<TDocument>
    where TDocument : class, new()
{
    private readonly SettingsPackOptions _options;
    private readonly ISettingsPackLoader _loader;
    private readonly ISettingsDocumentSerializer<TDocument> _serializer;
    private readonly object _gate = new();

    public SettingsPackCatalog(
        SettingsPackOptions options,
        ISettingsPackLoader loader,
        ISettingsDocumentSerializer<TDocument> serializer)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public event EventHandler? PackCacheChanged;

    public IReadOnlyList<SettingsPackInfo> ListInstalled()
    {
        if (string.IsNullOrWhiteSpace(_options.PacksDirectory))
            return [];

        Directory.CreateDirectory(_options.PacksDirectory);
        var results = new List<SettingsPackInfo>();

        foreach (var path in Directory.EnumerateFileSystemEntries(_options.PacksDirectory))
        {
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
                continue;

            if (File.Exists(path) && !fileName.EndsWith(_options.Extension, StringComparison.OrdinalIgnoreCase))
                continue;

            var load = _loader.LoadAsync(path).GetAwaiter().GetResult();
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

        var load = _loader.LoadAsync(packPath).GetAwaiter().GetResult();
        if (load?.OverlayJson is null)
            return null;

        return _serializer.Deserialize(load.OverlayJson, defaults);
    }

    internal void InvalidateCache()
    {
        lock (_gate)
        {
            if (Directory.Exists(_options.CacheDirectory))
                Directory.Delete(_options.CacheDirectory, recursive: true);
        }

        PackCacheChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? ResolvePackPath(string packId)
    {
        if (string.IsNullOrWhiteSpace(_options.PacksDirectory))
            return null;

        Directory.CreateDirectory(_options.PacksDirectory);
        var direct = Path.Combine(_options.PacksDirectory, packId);
        if (Directory.Exists(direct) || File.Exists(direct))
            return direct;

        if (!string.IsNullOrEmpty(_options.Extension)
            && !packId.EndsWith(_options.Extension, StringComparison.OrdinalIgnoreCase))
        {
            var withExtension = Path.Combine(_options.PacksDirectory, packId + _options.Extension);
            if (Directory.Exists(withExtension) || File.Exists(withExtension))
                return withExtension;
        }

        return null;
    }
}
