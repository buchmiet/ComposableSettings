using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public sealed class SettingsDIGeneratorTests(ITestOutputHelper output) : GeneratorBaseClass(output)
{
    [Fact]
    public void Generates_di_registration_for_root_and_component_classes()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            [SettingsRoot("root")]
            public sealed partial class RootViewModel
            {
                [SettingsChild("child")]
                public ChildViewModel Child { get; private set; } = null!;
            }

            [SettingsComponent("child")]
            public sealed class ChildViewModel { }
            """);

        Assert.Empty(diagnostics);

        var diSource = generatedSources.Values
            .First(s => s.Contains("AddGeneratedSettingsComponents"));
        Assert.Contains("services.AddTransient<global::TestNs.RootViewModel>();", diSource);
        Assert.Contains("services.AddTransient<global::TestNs.ChildViewModel>();", diSource);
    }

    [Fact]
    public void Does_not_register_classes_without_settings_attributes()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public class PlainClass { }

            [SettingsRoot("root")]
            public sealed partial class RootViewModel { }
            """);

        Assert.Empty(diagnostics);

        var diSource = generatedSources.Values
            .First(s => s.Contains("AddGeneratedSettingsComponents"));
        Assert.Contains("RootViewModel", diSource);
        Assert.DoesNotContain("PlainClass", diSource);
    }

    [Fact]
    public void Generates_transient_registration_for_all_component_types()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            [SettingsRoot("gui")]
            public sealed partial class MainViewModel { }

            [SettingsComponent("appearance")]
            public sealed partial class AppearanceViewModel { }

            [SettingsComponent("clock")]
            public sealed class ClockViewModel { }

            [SettingsComponent("logRender")]
            public sealed class LogRenderViewModel { }
            """);

        Assert.Empty(diagnostics);

        var diSource = generatedSources.Values
            .First(s => s.Contains("AddGeneratedSettingsComponents"));
        Assert.Contains("AddTransient<global::TestNs.MainViewModel>", diSource);
        Assert.Contains("AddTransient<global::TestNs.AppearanceViewModel>", diSource);
        Assert.Contains("AddTransient<global::TestNs.ClockViewModel>", diSource);
        Assert.Contains("AddTransient<global::TestNs.LogRenderViewModel>", diSource);
    }
}
