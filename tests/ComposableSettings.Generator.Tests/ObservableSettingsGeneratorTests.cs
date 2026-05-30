using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public class ObservableSettingsGeneratorTests(ITestOutputHelper output) : GeneratorBaseClass(output)
{
    [Fact]
    public void Emits_settings_passthrough_init_and_proxy_bodies_without_owning_INPC()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            [SettingsModel]
            public partial class ClockSettings
            {
                private bool _isGlslEnabled = true;
                private string _baseColor = "#e6194b";
            }

            [ObservableSettings(typeof(ClockSettings))]
            public partial class ClockViewModel : ObservableObjectStub
            {
                public ClockViewModel(ISettingsProvider<ClockSettings> p) => InitializeSettings(p);

                [SettingsProxy] public partial bool IsGlslEnabled { get; set; }
            }
            """;

        var (diagnostics, sources) = CompileAndRunObservableSettingsGenerator(source);

        // The whole thing composes and compiles end-to-end (model + consumer + proxy).
        Assert.Empty(diagnostics);

        var generated = Assert.Single(sources.Values, s => s.Contains("InitializeSettings"));
        Assert.Contains("public Demo.ClockSettings Settings =>", generated.Replace("global::", ""));
        Assert.Contains("public partial bool IsGlslEnabled", generated);
        Assert.Contains("get => Settings.IsGlslEnabled;", generated);
        Assert.Contains("set => Settings.IsGlslEnabled = value;", generated);
        // Relays into the EXISTING INPC instead of declaring its own event.
        Assert.Contains("OnPropertyChanged(nameof(Settings));", generated);
        Assert.DoesNotContain("event", generated);
    }

    [Fact]
    public void Does_not_trip_CSP024_on_an_observable_object()
    {
        // The whole point: a VM that ALREADY implements INPC must be accepted.
        const string source = """
            using ComposableSettings;

            namespace Demo;

            [SettingsModel]
            public partial class ClockSettings { private bool _isGlslEnabled = true; }

            [ObservableSettings(typeof(ClockSettings))]
            public partial class ClockViewModel : ObservableObjectStub
            {
                public ClockViewModel(ISettingsProvider<ClockSettings> p) => InitializeSettings(p);
            }
            """;

        var (diagnostics, _) = CompileAndRunObservableSettingsGenerator(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CSP024");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Reports_CSP030_when_consumer_is_not_partial()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            [SettingsModel]
            public partial class ClockSettings { private bool _isGlslEnabled; }

            [ObservableSettings(typeof(ClockSettings))]
            public class NotPartialVm : ObservableObjectStub { }
            """;

        var (diagnostics, _) = CompileAndRunObservableSettingsGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP030");
    }
}
