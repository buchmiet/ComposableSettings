using Microsoft.CodeAnalysis;
using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public class SettingsProxyValidationGeneratorTests(ITestOutputHelper output) : GeneratorBaseClass(output)
{
    [Fact]
    public void Reports_CSP046_for_orphan_settings_proxy()
    {
        const string source = """
            using ComposableSettings.Attributes;

            namespace Demo;

            public class AppDocument { public string ThemeId { get; set; } = ""; }

            public partial class OrphanVm : ObservableObjectStub
            {
                [SettingsProxy]
                public partial string ThemeId { get; set; }
            }
            """;

        var (diagnostics, _) = CompileAndRunSettingsProxyValidation(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP046" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Reports_CSP048_for_orphan_settings_draft_root()
    {
        const string source = """
            using ComposableSettings.Attributes;

            namespace Demo;

            [SettingsDraftRoot("Editor")]
            public partial class OrphanRootVm : ObservableObjectStub { }
            """;

        var (diagnostics, _) = CompileAndRunSettingsProxyValidation(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP048" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Reports_CSP041_when_settings_vm_and_draft_vm_combined()
    {
        const string source = """
            using ComposableSettings.Attributes;

            namespace Demo;

            [SettingsModel]
            public partial class ClockSettings { private bool _enabled = true; }

            [SettingsVm(typeof(ClockSettings))]
            [SettingsDraftVm(typeof(ClockSettings))]
            public partial class ConflictedVm : ObservableObjectStub { }
            """;

        var (diagnostics, _) = CompileAndRunObservableSettingsGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP041");
    }

    [Fact]
    public void Reports_CSP047_when_proxy_is_not_partial_on_draft_vm()
    {
        const string source = """
            using ComposableSettings.Attributes;

            namespace Demo;

            public class AppDocument { public string ThemeId { get; set; } = ""; }

            [SettingsDraftVm(typeof(AppDocument))]
            public partial class BadVm : ObservableObjectStub
            {
                [SettingsProxy]
                public string ThemeId { get; set; } = "";
            }
            """;

        var (diagnostics, _) = CompileAndRunSettingsDraftVmGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP047");
    }

    [Fact]
    public void Does_not_report_CSP046_when_proxy_is_on_settings_draft_vm()
    {
        const string source = """
            using ComposableSettings.Attributes;

            namespace Demo;

            public class AppDocument { public string ThemeId { get; set; } = ""; }

            [SettingsDraftVm(typeof(AppDocument))]
            public partial class GoodVm : ObservableObjectStub
            {
                [SettingsProxy]
                public partial string ThemeId { get; set; }
            }
            """;

        var (diagnostics, _) = CompileAndRunSettingsDraftVmGenerator(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CSP046");
    }
}
