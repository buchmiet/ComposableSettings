namespace ComposableSettings.Document;

/// <summary>
/// Preview / commit / cancel helpers for settings editors — no MVVM toolkit dependency.
/// Wrap in <c>[RelayCommand]</c> in the host app.
/// </summary>
public static class SettingsDraftCommands
{
    public static void Preview<TDocument>(SettingsEditingSession<TDocument> session, ISettingsDocumentStore<TDocument> store)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(store);

        session.Touch();
        store.Preview(session.Draft);
    }

    public static async Task CommitAsync<TDocument>(
        SettingsEditingSession<TDocument> session,
        ISettingsDocumentStore<TDocument> store,
        CancellationToken cancellationToken = default)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(store);

        await store.CommitAsync(session.Draft, cancellationToken).ConfigureAwait(false);
        session.UpdateBaseline();
    }

    public static void Cancel<TDocument>(
        SettingsEditingSession<TDocument> session,
        ISettingsDocumentStore<TDocument> store,
        Action refreshUi)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(refreshUi);

        session.ResetFromBaseline();
        store.Preview(session.Draft);
        refreshUi();
    }

    public static void ResetSection<TDocument>(
        SettingsEditingSession<TDocument> session,
        ISettingsDocumentStore<TDocument> store,
        Action<TDocument> resetSection,
        Action refreshUi)
        where TDocument : class, new()
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resetSection);
        ArgumentNullException.ThrowIfNull(refreshUi);

        resetSection(session.Draft);
        Preview(session, store);
        refreshUi();
    }
}
