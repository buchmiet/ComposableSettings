namespace ComposableSettings;

/// <summary>Options for <see cref="SettingsProvider{TSettings}"/>.</summary>
public  class SettingsProviderOptions
{
    public static SettingsProviderOptions Default { get; } = new();

    /// <summary>
    /// Optional delay used to coalesce writes to the backing store.
    /// The live settings object is still mutated synchronously; only persistence is delayed.
    /// </summary>
    public TimeSpan? PersistDebounceDelay { get; init; }
}
