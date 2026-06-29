namespace ComposableSettings.Document;

/// <summary>
/// Document-profile settings: effective merged view, user layer on disk,
/// preview without persist, explicit commit.
/// </summary>
public interface ISettingsDocumentStore<TDocument>
    where TDocument : class, new()
{
    /// <summary>Merged effective settings (defaults + user layer).</summary>
    TDocument Effective { get; }

    /// <summary>User-owned slice persisted to disk.</summary>
    TDocument UserLayer { get; }

    /// <summary>Raised when <see cref="Effective"/> changes.</summary>
    event EventHandler? EffectiveChanged;

    /// <summary>In-memory preview; does not write to disk.</summary>
    void Preview(TDocument userLayerDraft);

    /// <summary>Persist user layer (debounced when configured).</summary>
    Task CommitAsync(TDocument userLayerDraft, CancellationToken cancellationToken = default);

    /// <summary>Flush any pending debounced commit immediately.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Re-read user file and rebuild effective.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>Clear user layer to empty document, persist, rebuild effective from defaults.</summary>
    Task ResetUserLayerAsync(CancellationToken cancellationToken = default);
}
