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

    [Fact]
    public void Generates_observable_collection_passthrough_and_constructor_hook()
    {
        const string source = """
            using System.Collections.ObjectModel;
            using ComposableSettings;

            namespace Demo;

            [SettingsModel]
            public partial class PaletteSettings
            {
                private ObservableCollection<string> _colors = new() { "#a", "#b" };
                private int _maxColors = 8;
            }
            """;

        var (diagnostics, sources) = CompileAndRunObservableGenerators(source);

        Assert.Empty(diagnostics); // get-only collection + ctor hook + scalar all compile
        Assert.Contains(sources.Values, s =>
            s.Contains("Colors =>")
            && s.Contains("CollectionChanged")
            && s.Contains("MaxColors"));
    }
}
