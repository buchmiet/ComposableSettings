using System.ComponentModel;

namespace ComposableSettings;

/// <summary>
/// What a consumer (ViewModel/component) receives by DI. It does NOT expose
/// Get/Set/Save — the consumer is handed a single LIVE, observable instance and
/// stores no settings state of its own.
///
/// Writes auto-persist (the provider subscribes to the instance's
/// <see cref="INotifyPropertyChanged"/>); resets/reloads swap the instance and
/// raise <see cref="Replaced"/> so consumers can re-hook.
/// </summary>
public interface ISettingsProvider<TSettings>
    where TSettings : class, INotifyPropertyChanged, new()
{
    /// <summary>The live, observable settings instance.</summary>
    TSettings Current { get; }

    /// <summary>Raised when the instance is REPLACED (reset / external reload).</summary>
    event EventHandler<TSettings>? Replaced;

    /// <summary>Restore defaults (new instance + persist + <see cref="Replaced"/>).</summary>
    void Reset();

    /// <summary>Reload from the backing file (simulates "someone else wrote it").</summary>
    void Reload();
}
