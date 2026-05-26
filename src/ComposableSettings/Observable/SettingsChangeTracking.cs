using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ComposableSettings;

/// <summary>
/// Runtime support used by generated settings models to propagate nested changes
/// up to the owning model's <see cref="INotifyPropertyChanged.PropertyChanged"/>,
/// so the provider auto-persists on edits at any depth.
///
///  - <see cref="Track"/>/<see cref="Untrack"/>: a single nested child object.
///  - <see cref="TrackCollection"/>: an observable collection — add/remove AND
///    in-place edits of items that are themselves observable.
///
/// The change chain works for arbitrary depth: a grandchild change raises the
/// child (its model tracks it), which raises the parent (this tracks the child),
/// up to the root the provider watches.
/// </summary>
public static class SettingsChangeTracking
{
    public static void Track(object? child, PropertyChangedEventHandler handler)
    {
        if (child is INotifyPropertyChanged notifier)
            notifier.PropertyChanged += handler;
    }

    public static void Untrack(object? child, PropertyChangedEventHandler handler)
    {
        if (child is INotifyPropertyChanged notifier)
            notifier.PropertyChanged -= handler;
    }

    /// <summary>
    /// Subscribes to the collection's add/remove and to each item's
    /// <see cref="INotifyPropertyChanged"/>; invokes <paramref name="onChanged"/>
    /// on any of those. Item subscriptions are re-synced on every collection change
    /// (correct across add/remove/replace/clear, no leaks).
    /// </summary>
    public static void TrackCollection(INotifyCollectionChanged collection, Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(onChanged);
        _ = new CollectionTracker(collection, onChanged);
    }

    private sealed class CollectionTracker
    {
        private readonly INotifyCollectionChanged _collection;
        private readonly Action _onChanged;
        private readonly HashSet<INotifyPropertyChanged> _items = new();

        public CollectionTracker(INotifyCollectionChanged collection, Action onChanged)
        {
            _collection = collection;
            _onChanged = onChanged;
            Resync();
            // Keeps this tracker alive for the collection's lifetime (the model owns it).
            collection.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Resync();
            _onChanged();
        }

        private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => _onChanged();

        private void Resync()
        {
            var current = new HashSet<INotifyPropertyChanged>();
            if (_collection is IEnumerable items)
                foreach (var item in items)
                    if (item is INotifyPropertyChanged notifier)
                        current.Add(notifier);

            foreach (var gone in _items.Where(i => !current.Contains(i)).ToList())
            {
                gone.PropertyChanged -= OnItemChanged;
                _items.Remove(gone);
            }

            foreach (var added in current.Where(i => !_items.Contains(i)))
            {
                added.PropertyChanged += OnItemChanged;
                _items.Add(added);
            }
        }
    }
}
