using ComposableSettings.Layering;
using ComposableSettings.Packs;
using ComposableSettings.Stores;
using IDebouncer = Debouncer.Sharp.IDebouncer;
using SharpDebouncer = Debouncer.Sharp.Debouncer;

namespace ComposableSettings.Document;

/// <summary>
/// Default <see cref="ISettingsDocumentStore{TDocument}"/>: user layer on disk,
/// effective = defaults merged with user, preview without persist, debounced commit.
/// </summary>
public sealed class SettingsDocumentStore<TDocument> : ISettingsDocumentStore<TDocument>, IDisposable
    where TDocument : class, new()
{
    private readonly SettingsDocumentOptions<TDocument> _options;
    private readonly ISettingsDocumentSerializer<TDocument> _serializer;
    private readonly ISettingsLayerMerger<TDocument>? _merger;
    private readonly ISettingsPackCatalog<TDocument>? _packCatalog;
    private readonly Func<TDocument, string?>? _packIdResolver;
    private readonly IDebouncer _commitDebouncer;
    private readonly object _gate = new();
    private TDocument _userLayer = new();
    private TDocument _effective = new();
    private TDocument? _pendingCommit;
    private bool _disposed;

    public SettingsDocumentStore(SettingsDocumentOptions<TDocument> options)
        : this(options, merger: null, packCatalog: null, packIdResolver: null)
    {
    }

    internal SettingsDocumentStore(
        SettingsDocumentOptions<TDocument> options,
        ISettingsLayerMerger<TDocument>? merger,
        ISettingsPackCatalog<TDocument>? packCatalog,
        Func<TDocument, string?>? packIdResolver)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.FilePath))
            throw new ArgumentException("FilePath is required.", nameof(options));
        if (_options.DefaultsFactory is null)
            throw new ArgumentException("DefaultsFactory is required.", nameof(options));
        if (_options.AutosaveDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "AutosaveDelay must be greater than zero.");

        _merger = merger;
        _packCatalog = packCatalog;
        _packIdResolver = packIdResolver;
        _serializer = _options.Serializer ?? new JsonSettingsDocumentSerializer<TDocument>();
        _commitDebouncer = new SharpDebouncer(_options.AutosaveDelay);
        (_userLayer, _effective) = LoadInitialState();
    }

    public TDocument Effective
    {
        get
        {
            lock (_gate)
                return _serializer.Clone(_effective);
        }
    }

    public TDocument UserLayer
    {
        get
        {
            lock (_gate)
                return _serializer.Clone(_userLayer);
        }
    }

    public event EventHandler? EffectiveChanged;

    public void Preview(TDocument userLayerDraft)
    {
        ArgumentNullException.ThrowIfNull(userLayerDraft);

        lock (_gate)
        {
            _userLayer = _serializer.Clone(userLayerDraft);
            _options.Normalize?.Invoke(_userLayer);
            RebuildEffectiveLocked();
            EffectiveChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Task CommitAsync(TDocument userLayerDraft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userLayerDraft);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _pendingCommit = _serializer.Clone(userLayerDraft);
            _options.Normalize?.Invoke(_pendingCommit);
            _userLayer = _serializer.Clone(_pendingCommit);
            RebuildEffectiveLocked();
            EffectiveChanged?.Invoke(this, EventArgs.Empty);
        }

        _ = _commitDebouncer.HitAsync(_ =>
        {
            FlushPendingCommit();
            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _commitDebouncer.Cancel();
        FlushPendingCommit();
        return Task.CompletedTask;
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _commitDebouncer.Cancel();

        lock (_gate)
        {
            _pendingCommit = null;
            (_userLayer, _effective) = LoadFromDiskLocked();
            EffectiveChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    public Task ResetUserLayerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _commitDebouncer.Cancel();

        lock (_gate)
        {
            _pendingCommit = null;
            _userLayer = new TDocument();
            RebuildEffectiveLocked();
            PersistUserLayerLocked(_userLayer);
            EffectiveChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    internal void ReloadFromPackCache()
    {
        lock (_gate)
        {
            RebuildEffectiveLocked();
            EffectiveChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _commitDebouncer.Cancel();
        FlushPendingCommit();
        _commitDebouncer.Dispose();
    }

    private (TDocument User, TDocument Effective) LoadInitialState()
    {
        lock (_gate)
            return LoadFromDiskLocked();
    }

    private (TDocument User, TDocument Effective) LoadFromDiskLocked()
    {
        var defaults = _options.DefaultsFactory();
        var user = ReadUserLayerFromDisk(defaults);
        _options.Normalize?.Invoke(user);
        var effective = BuildEffective(defaults, user);
        return (user, effective);
    }

    private TDocument ReadUserLayerFromDisk(TDocument defaults)
    {
        if (!File.Exists(_options.FilePath))
            return new TDocument();

        try
        {
            var json = File.ReadAllText(_options.FilePath);
            return _serializer.Deserialize(json, defaults);
        }
        catch
        {
            return new TDocument();
        }
    }

    private void RebuildEffectiveLocked()
    {
        var defaults = _options.DefaultsFactory();
        _effective = BuildEffective(defaults, _userLayer);
    }

    private TDocument BuildEffective(TDocument defaults, TDocument userLayer)
    {
        var packOverlay = ResolvePackOverlay(defaults, userLayer);

        if (_merger is not null)
            return _merger.Merge(defaults, packOverlay, userLayer);

        if (IsEffectivelyEmptyUserLayer(userLayer))
        {
            return packOverlay is null
                ? _serializer.Clone(defaults)
                : JsonDocumentMerge.MergePackOverlay(defaults, packOverlay);
        }

        var withPack = packOverlay is null
            ? defaults
            : JsonDocumentMerge.MergePackOverlay(defaults, packOverlay);

        return JsonDocumentMerge.MergeOverlay(withPack, userLayer);
    }

    private TDocument? ResolvePackOverlay(TDocument defaults, TDocument userLayer)
    {
        if (_packCatalog is null || _packIdResolver is null)
            return null;

        var packId = _packIdResolver(userLayer);
        if (string.IsNullOrWhiteSpace(packId))
            return null;

        return _packCatalog.TryLoadOverlay(packId, defaults);
    }

    private bool IsEffectivelyEmptyUserLayer(TDocument user)
    {
        var empty = new TDocument();
        return _serializer.Serialize(user) == _serializer.Serialize(empty);
    }

    private void FlushPendingCommit()
    {
        TDocument? toWrite;
        lock (_gate)
        {
            toWrite = _pendingCommit is null ? null : _serializer.Clone(_pendingCommit);
            _pendingCommit = null;
        }

        if (toWrite is null)
            return;

        lock (_gate)
            PersistUserLayerLocked(toWrite);
    }

    private void PersistUserLayerLocked(TDocument userLayer)
    {
        var json = _serializer.Serialize(userLayer);
        if (_options.UseAtomicWrites)
            AtomicFileWrite.WriteAllText(_options.FilePath, json);
        else
            File.WriteAllText(_options.FilePath, json);
    }
}
