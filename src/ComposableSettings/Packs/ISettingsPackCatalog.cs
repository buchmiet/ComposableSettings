namespace ComposableSettings.Packs;

public interface ISettingsPackCatalog<TDocument>
    where TDocument : class, new()
{
    IReadOnlyList<SettingsPackInfo> ListInstalled();

    TDocument? TryLoadOverlay(string packId, TDocument defaults);

    event EventHandler? PackCacheChanged;
}
