namespace ComposableSettings.Document;

/// <summary>
/// Draft/baseline helper for settings editors (preview/commit workflow).
/// </summary>
public sealed class SettingsEditingSession<TDocument>
    where TDocument : class, new()
{
    private readonly ISettingsDocumentSerializer<TDocument> _serializer;
    private TDocument _baseline;

    public SettingsEditingSession(
        TDocument baseline,
        ISettingsDocumentSerializer<TDocument>? serializer = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        _serializer = serializer ?? new JsonSettingsDocumentSerializer<TDocument>();
        _baseline = _serializer.Clone(baseline);
        Draft = _serializer.Clone(baseline);
    }

    public TDocument Draft { get; private set; }

    public int ChangeRevision { get; private set; }

    public void Touch() => ChangeRevision++;

    public void UpdateBaseline()
    {
        _baseline = _serializer.Clone(Draft);
    }

    public void ResetFromBaseline()
    {
        Draft = _serializer.Clone(_baseline);
        Touch();
    }

    public TDocument CloneBaseline() => _serializer.Clone(_baseline);
}
