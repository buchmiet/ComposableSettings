using ComposableSettings.Document;

namespace ComposableSettings.Tests;

public sealed class SettingsDraftCommandsTests
{
    public sealed class CmdDocument
    {
        public string Name { get; set; } = "default";
    }

  private static (SettingsEditingSession<CmdDocument> Session, TestDocumentStore Store) Create()
    {
        var store = new TestDocumentStore();
        var session = new SettingsEditingSession<CmdDocument>(store.Effective);
        return (session, store);
    }

    [Fact]
    public void Cancel_restores_baseline_and_previews()
    {
        var (session, store) = Create();
        session.Draft.Name = "edited";
        SettingsDraftCommands.Preview(session, store);

        var refreshed = false;
        SettingsDraftCommands.Cancel(session, store, () => refreshed = true);

        Assert.Equal("default", session.Draft.Name);
        Assert.Equal("default", store.LastPreview?.Name);
        Assert.True(refreshed);
    }

    [Fact]
    public async Task CommitAsync_updates_baseline_and_persists()
    {
        var (session, store) = Create();
        session.Draft.Name = "saved";

        await SettingsDraftCommands.CommitAsync(session, store);

        Assert.Equal("saved", session.Draft.Name);
        Assert.Equal("saved", store.LastCommit?.Name);
        session.Draft.Name = "transient";
        session.ResetFromBaseline();
        Assert.Equal("saved", session.Draft.Name);
    }

    private sealed class TestDocumentStore : ISettingsDocumentStore<CmdDocument>
    {
        private CmdDocument _effective = new();
        private CmdDocument _user = new();

        public CmdDocument? LastPreview { get; private set; }
        public CmdDocument? LastCommit { get; private set; }

        public CmdDocument Effective => _effective;
        public CmdDocument UserLayer => _user;
        public event EventHandler? EffectiveChanged;

        public void Preview(CmdDocument userLayerDraft)
        {
            LastPreview = userLayerDraft;
            _user = userLayerDraft;
            _effective = userLayerDraft;
            EffectiveChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task CommitAsync(CmdDocument userLayerDraft, CancellationToken cancellationToken = default)
        {
            LastCommit = userLayerDraft;
            Preview(userLayerDraft);
            return Task.CompletedTask;
        }

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetUserLayerAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
