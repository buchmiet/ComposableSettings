namespace ComposableSettings.Layering;

public interface ISettingsLayerMerger<TDocument>
    where TDocument : class, new()
{
    TDocument Merge(TDocument defaults, TDocument? packOverlay, TDocument userLayer);
}
