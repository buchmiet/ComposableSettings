using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public class SettingsConsumerGeneratorTests(ITestOutputHelper output) : GeneratorBaseClass(output)
{
    [Fact]
    public void Generates_provider_settings_passthrough_and_init_method()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            [SettingsModel]
            public partial class RuntimeSettings
            {
                private int _maxConcurrentRuns = 2;
            }

            [SettingsConsumer(typeof(RuntimeSettings))]
            public partial class EngineComponent
            {
                public EngineComponent(ISettingsProvider<RuntimeSettings> provider)
                    => InitializeGeneratedSettings(provider);
            }
            """;

        var (diagnostics, sources) = CompileAndRunObservableGenerators(source);

        Assert.Empty(diagnostics); // model + consumer compose and compile end-to-end
        Assert.Contains(sources.Values, s =>
            s.Contains("InitializeGeneratedSettings")
            && s.Contains("Settings =>"));
    }

    [Fact]
    public void Reports_CSP022_when_consumer_is_not_partial()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            [SettingsModel]
            public partial class RuntimeSettings { private int _x; }

            [SettingsConsumer(typeof(RuntimeSettings))]
            public class NotPartialConsumer { }
            """;

        var (diagnostics, _) = CompileAndRunObservableGenerators(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP022");
    }
}
