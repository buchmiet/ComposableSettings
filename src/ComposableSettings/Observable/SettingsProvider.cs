using System.ComponentModel;
using ComposableSettings.Runtime;

namespace ComposableSettings;

/// <summary>
/// Default <see cref="ISettingsProvider{TSettings}"/>: holds one live instance,
/// subscribes to its <see cref="INotifyPropertyChanged"/> and AUTO-PERSISTS each
/// change to a backing <see cref="IComponentSettingsProvider"/> (one file) at a
/// node path.
///
/// Multiple files = multiple backing providers (one per owner): the runtime host
/// registers its file, the GUI host registers its own. Neither knows the other.
/// (Persistence debounce is intentionally deferred — every change writes now.)
/// </summary>
public sealed class SettingsProvider<TSettings> : ISettingsProvider<TSettings>, IDisposable
    where TSettings : class, INotifyPropertyChanged, new()
{
    private readonly IComponentSettingsProvider _file;
    private readonly SettingsNodePath _node;
    private TSettings _current = null!;

    public SettingsProvider(IComponentSettingsProvider file, SettingsNodePath node)
    {
        _file = file ?? throw new ArgumentNullException(nameof(file));
        _node = node ?? throw new ArgumentNullException(nameof(node));
        Adopt(_file.Get<TSettings>(node));
    }

    public TSettings Current => _current;

    public event EventHandler<TSettings>? Replaced;

    public void Reset()
    {
        Adopt(new TSettings());
        _file.Set(_node, _current);
        Replaced?.Invoke(this, _current);
    }

    public void Reload()
    {
        Adopt(_file.Get<TSettings>(_node));
        Replaced?.Invoke(this, _current);
    }

    public void Dispose()
    {
        if (_current is not null)
        {
            _current.PropertyChanged -= OnCurrentChanged;
        }
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
        => _file.Set(_node, _current);
}
