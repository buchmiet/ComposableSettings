using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public class SettingsModelGeneratorTests(ITestOutputHelper output) : GeneratorBaseClass(output)
{
    [Fact]
    public void Generates_observable_properties_from_underscore_fields()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            [SettingsModel]
            public partial class RuntimeSettings
            {
                private string _pluginsFolder = "./plugins";
                private int _maxConcurrentRuns = 2;
                private bool _verbose;
            }
            """;

        var (diagnostics, sources) = CompileAndRunObservableGenerators(source);

        Assert.Empty(diagnostics); // the generated INPC model compiles cleanly
        Assert.Contains(sources.Values, s =>
            s.Contains("PluginsFolder")
            && s.Contains("MaxConcurrentRuns")
            && s.Contains("Verbose")
            && s.Contains("INotifyPropertyChanged"));
    }

    [Fact]
    public void Reports_CSP020_when_model_is_not_partial()
    {
        const string source = """
            using ComposableSettings;

            namespace Demo;

            [SettingsModel]
            public class NotPartial
            {
                private int _value;
            }
            """;

        var (diagnostics, _) = CompileAndRunObservableGenerators(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP020");
    }
}
