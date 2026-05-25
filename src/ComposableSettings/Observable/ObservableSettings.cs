using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ComposableSettings;

/// <summary>
/// Base class for observable (canonical) settings models.
///
/// Provides <see cref="INotifyPropertyChanged"/> plumbing so any property change
/// is observable — which is what lets <see cref="ISettingsProvider{TSettings}"/>
/// auto-persist and two-way bindings refresh immediately.
///
/// Hand-written models derive from this. (A future source generator can emit the
/// same shape for <c>[SettingsModel] partial</c> models so consumers don't write it.)
/// </summary>
public abstract class ObservableSettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
