using ComposableSettings.Layering;
using ComposableSettings.Packs;
using Debouncer.Sharp;
using Debouncer.Sharp.Contracts;
using Debouncer.Sharp.FileSystem;

namespace ComposableSettings.Document;

/// <summary>
/// Default <see cref="ISettingsDocumentStore{TDocument}"/>: user layer on disk,
/// effective = defaults merged with user, preview without persist, debounced commit.
/// </summary>
public  class SettingsDocumentStore<TDocument> : ISettingsDocumentStore<TDocument>, IDisposable
    where TDocument : class, new()
{
    private readonly SettingsDocumentOptions<TDocument> _options;
    private readonly ISettingsDocumentSerializer<TDocument> _serializer;
    private readonly ISettingsLayerMerger<TDocument>? _merger;
    private readonly ISettingsPackCatalog<TDocument>? _packCatalog;
    private readonly Func<TDocument, string?>? _packIdResolver;
    private readonly IDebouncedLatest<TDocument> _snapshotSink;
    private readonly Lazy<byte[]> _emptyUserLayerUtf8;
    private readonly Lock _gate = new();
    private TDocument _userLayer = new();
    private TDocument _effective = new();
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
        _emptyUserLayerUtf8 = new Lazy<byte[]>(() => _serializer.SerializeUtf8(new TDocument()));

        var factory = new DebounceFactory();
        _snapshotSink = _options.UseAtomicWrites
            ? factory.CreateSnapshot<TDocument>(_options.FilePath, _options.AutosaveDelay, _serializer.Serialize)
            : factory.CreateLatest<TDocument>(_options.AutosaveDelay, (doc, _) =>
            {
                Utf8SettingsFile.WriteAllBytes(_options.FilePath, _serializer.SerializeUtf8(doc));
                return Task.CompletedTask;
            });

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

    public ValueTask CommitAsync(TDocument userLayerDraft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userLayerDraft);
        cancellationToken.ThrowIfCancellationRequested();

        TDocument snapshot;
        lock (_gate)
        {
            var normalized = _serializer.Clone(userLayerDraft);
            _options.Normalize?.Invoke(normalized);
            _userLayer = _serializer.Clone(normalized);
            RebuildEffectiveLocked();
            EffectiveChanged?.Invoke(this, EventArgs.Empty);
            snapshot = _serializer.Clone(_userLayer);
        }

        // The sink serializes on its own schedule, outside this lock — hand it an independent
        // clone so a later Preview/CommitAsync can't mutate the value while it's pending.
        _snapshotSink.Hit(snapshot);
        return ValueTask.CompletedTask;
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _snapshotSink.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask ReloadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _snapshotSink.Cancel();

        lock (_gate)
        {
            (_userLayer, _effective) = LoadFromDiskLocked();
            EffectiveChanged?.Invoke(this, EventArgs.Empty);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask ResetUserLayerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _snapshotSink.Cancel();

        TDocument snapshot;
        lock (_gate)
        {
            _userLayer = new TDocument();
            RebuildEffectiveLocked();
            snapshot = _serializer.Clone(_userLayer);
            EffectiveChanged?.Invoke(this, EventArgs.Empty);
        }

        // Reset persists immediately rather than waiting out the debounce window: hit then flush.
        _snapshotSink.Hit(snapshot);
        await _snapshotSink.FlushAsync(cancellationToken).ConfigureAwait(false);
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

        // IDebouncedLatest.Dispose() only cancels the pending timer, by design (it avoids a
        // sync-over-async deadlock risk when a caller marshals onto a UI thread). This store has
        // no such caller and existing consumers rely on Dispose flushing the last commit, so
        // block on FlushAsync here deliberately before disposing the sink.
        try
        {
            _snapshotSink.FlushAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Dispose() must not throw and must not leak the sink's timer/semaphore. A caller
            // that needs to observe a failed final write should call FlushAsync() explicitly
            // before disposing — that path still propagates exceptions normally.
        }
        finally
        {
            _snapshotSink.Dispose();
        }
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
            var utf8Json = Utf8SettingsFile.ReadAllBytes(_options.FilePath);
            return _serializer.Deserialize(utf8Json, defaults);
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
        => _serializer.SerializeUtf8(user).AsSpan().SequenceEqual(_emptyUserLayerUtf8.Value);
}
