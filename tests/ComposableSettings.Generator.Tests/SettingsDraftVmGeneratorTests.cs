using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public class SettingsDraftVmGeneratorTests(ITestOutputHelper output) : GeneratorBaseClass(output)
{
    [Fact]
    public void Emits_draft_init_nested_proxies_and_preview_on_set()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            public class EditorSection
            {
                public double FontSize { get; set; } = 12;
                public bool ShowLineNumbers { get; set; }
            }

            public class AppDocument
            {
                public EditorSection Editor { get; set; } = new();
            }

            [SettingsDraftVm(typeof(AppDocument))]
            [SettingsDraftRoot("Editor")]
            public partial class EditorSettingsViewModel : ObservableObjectStub
            {
                public EditorSettingsViewModel(
                    global::ComposableSettings.Document.SettingsEditingSession<AppDocument> session,
                    global::ComposableSettings.Document.ISettingsDocumentStore<AppDocument> store)
                    => InitializeSettingsDraft(session, store);

                [SettingsProxy("FontSize")]
                public partial double FontSize { get; set; }

                [SettingsProxy("ShowLineNumbers")]
                public partial bool ShowLineNumbers { get; set; }
            }
            """;

        var (diagnostics, sources) = CompileAndRunSettingsDraftVmGenerator(source);

        Assert.Empty(diagnostics);

        var generated = Assert.Single(sources.Values, s => s.Contains("InitializeSettingsDraft"));
        Assert.Contains("private void InitializeSettingsDraft(", generated);
        Assert.Contains("get => Draft.Editor.FontSize;", generated);
        Assert.Contains("get => Draft.Editor.ShowLineNumbers;", generated);
        Assert.Contains("DraftMutation.TrySetDouble(", generated);
        Assert.Contains("DraftMutation.TrySet(", generated);
        Assert.Contains("_draftStore.Preview(_draftSession.Draft);", generated);
        Assert.Contains("store.EffectiveChanged += OnStoreEffectiveChanged;", generated);
        Assert.Contains("void DisposeGeneratedSettingsDraft()", generated);
    }

    [Fact]
    public void Emits_flat_proxy_at_document_root()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            public class AppDocument
            {
                public string ThemeId { get; set; } = "default";
            }

            [SettingsDraftVm(typeof(AppDocument))]
            public partial class ThemeSettingsViewModel : ObservableObjectStub
            {
                public ThemeSettingsViewModel(
                    global::ComposableSettings.Document.SettingsEditingSession<AppDocument> session,
                    global::ComposableSettings.Document.ISettingsDocumentStore<AppDocument> store)
                    => InitializeSettingsDraft(session, store);

                [SettingsProxy]
                public partial string ThemeId { get; set; }
            }
            """;

        var (diagnostics, sources) = CompileAndRunSettingsDraftVmGenerator(source);

        Assert.Empty(diagnostics);
        var generated = Assert.Single(sources.Values, s => s.Contains("InitializeSettingsDraft"));
        Assert.Contains("get => Draft.ThemeId;", generated);
    }

    [Fact]
    public void Reports_CSP044_when_not_partial()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            public class AppDocument { public string ThemeId { get; set; } = ""; }

            [SettingsDraftVm(typeof(AppDocument))]
            public class NotPartialVm : ObservableObjectStub { }
            """;

        var (diagnostics, _) = CompileAndRunSettingsDraftVmGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP044");
    }

    [Fact]
    public void Reports_CSP042_for_invalid_proxy_path()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            public class AppDocument { public string ThemeId { get; set; } = ""; }

            [SettingsDraftVm(typeof(AppDocument))]
            public partial class BadVm : ObservableObjectStub
            {
                [SettingsProxy("Missing.Path")]
                public partial string Broken { get; set; }
            }
            """;

        var (diagnostics, _) = CompileAndRunSettingsDraftVmGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP042");
    }

    [Fact]
    public void Reports_CSP043_for_proxy_type_mismatch()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            public class AppDocument { public string ThemeId { get; set; } = ""; }

            [SettingsDraftVm(typeof(AppDocument))]
            public partial class BadVm : ObservableObjectStub
            {
                [SettingsProxy("ThemeId")]
                public partial int ThemeId { get; set; }
            }
            """;

        var (diagnostics, _) = CompileAndRunSettingsDraftVmGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP043");
    }
}
