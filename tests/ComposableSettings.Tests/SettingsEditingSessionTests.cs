using ComposableSettings.Document;

namespace ComposableSettings.Tests;

public sealed class SettingsEditingSessionTests
{
    public sealed class EditDocument
    {
        public string ThemeId { get; set; } = "default";
        public int Width { get; set; }
    }

    [Fact]
    public void ResetFromBaseline_restores_draft_without_touching_baseline()
    {
        var session = new SettingsEditingSession<EditDocument>(new EditDocument { ThemeId = "base", Width = 100 });
        session.Draft.ThemeId = "edited";
        session.Draft.Width = 200;
        session.Touch();

        session.ResetFromBaseline();

        Assert.Equal("base", session.Draft.ThemeId);
        Assert.Equal(100, session.Draft.Width);
        Assert.Equal(2, session.ChangeRevision);
    }

    [Fact]
    public void UpdateBaseline_captures_current_draft()
    {
        var session = new SettingsEditingSession<EditDocument>(new EditDocument { ThemeId = "base" });
        session.Draft.ThemeId = "saved";
        session.UpdateBaseline();
        session.Draft.ThemeId = "unsaved";

        session.ResetFromBaseline();
        Assert.Equal("saved", session.Draft.ThemeId);
    }
}
