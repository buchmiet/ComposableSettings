using System.ComponentModel;
using ComposableSettings.Runtime;
using IDebouncer = Debouncer.Sharp.Contracts.IDebouncer;
using SharpDebouncer = Debouncer.Sharp.Debouncer;

namespace ComposableSettings;

/// <summary>
/// Default <see cref="ISettingsProvider{TSettings}"/>: holds one live instance,
/// subscribes to its <see cref="INotifyPropertyChanged"/> and AUTO-PERSISTS each
/// change to a backing <see cref="IComponentSettingsProvider"/> (one file) at a
/// node path.
///
/// Multiple files = multiple backing providers (one per owner): the runtime host
/// registers its file, the GUI host registers its own. Neither knows the other.
/// Persistence debounce is opt-in: the live instance changes immediately, while
/// writes to the backing store can be coalesced with <see cref="SettingsProviderOptions"/>.
/// </summary>
public sealed class SettingsProvider<TSettings> : ISettingsProvider<TSettings>, IDisposable
    where TSettings : class, INotifyPropertyChanged, new()
{
    private readonly IComponentSettingsProvider _file;
    private readonly SettingsNodePath _node;
    private readonly IDebouncer? _persistDebouncer;
    private readonly object _persistGate = new();
    private TSettings _current = null!;
    private bool _hasPendingPersist;
    private bool _disposed;

    public SettingsProvider(IComponentSettingsProvider file, SettingsNodePath node)
        : this(file, node, SettingsProviderOptions.Default)
    {
    }

    public SettingsProvider(
        IComponentSettingsProvider file,
        SettingsNodePath node,
        SettingsProviderOptions? options)
    {
        _file = file ?? throw new ArgumentNullException(nameof(file));
        _node = node ?? throw new ArgumentNullException(nameof(node));

        var persistDebounceDelay = options?.PersistDebounceDelay;
        if (persistDebounceDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Persist debounce delay must be greater than zero.");

        _persistDebouncer = persistDebounceDelay is { } delay ? new SharpDebouncer(delay) : null;
        Adopt(_file.Get<TSettings>(node));
    }

    public TSettings Current => _current;

    public event EventHandler<TSettings>? Replaced;

    public void Reset()
    {
        CancelPendingPersist();
        Adopt(new TSettings());
        PersistCurrent();
        Replaced?.Invoke(this, _current);
    }

    public void Reload()
    {
        CancelPendingPersist();
        Adopt(_file.Get<TSettings>(_node));
        Replaced?.Invoke(this, _current);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_current is not null)
        {
            _current.PropertyChanged -= OnCurrentChanged;
        }

        FlushPendingPersist();
        _persistDebouncer?.Dispose();
    }

    private void Adopt(TSettings instance)
    {
        if (_current is not null)
        {
            _current.PropertyChanged -= OnCurrentChanged;
        }

        _current = instance;
        _current.PropertyChanged += OnCurrentChanged;
    }

    private void OnCurrentChanged(object? sender, PropertyChangedEventArgs e)
        => PersistCurrentDebounced();

    private void PersistCurrentDebounced()
    {
        if (_persistDebouncer is null)
        {
            PersistCurrent();
            return;
        }

        _hasPendingPersist = true;
        _ = _persistDebouncer.HitAsync(ct =>
        {
            if (!ct.IsCancellationRequested)
                PersistCurrent();

            return Task.CompletedTask;
        });
    }

    private void PersistCurrent()
    {
        lock (_persistGate)
        {
            _file.Set(_node, _current);
            _hasPendingPersist = false;
        }
    }

    private void CancelPendingPersist()
    {
        _persistDebouncer?.Cancel();
        _hasPendingPersist = false;
    }

    private void FlushPendingPersist()
    {
        if (_persistDebouncer is null)
            return;

        _persistDebouncer.Cancel();
        if (_hasPendingPersist)
            PersistCurrent();
    }
}
