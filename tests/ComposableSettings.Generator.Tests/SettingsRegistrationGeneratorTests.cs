using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public sealed class SettingsRegistrationGeneratorTests(ITestOutputHelper output) : GeneratorBaseClass(output)
{
    [Fact]
    public void Generates_registration_for_leaf_components_with_settings_type()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class ClockSettings { }
            public sealed class LogRenderSettings { }

            [SettingsRoot("gui")]
            public sealed partial class RootViewModel
            {
                [SettingsChild("appearance")]
                public AppearanceViewModel Appearance { get; private set; } = null!;
            }

            [SettingsComponent("appearance")]
            public sealed partial class AppearanceViewModel
            {
                [SettingsChild("clock")]
                public ClockViewModel Clock { get; private set; } = null!;

                [SettingsChild("logRender")]
                public LogRenderViewModel LogRender { get; private set; } = null!;
            }

            [SettingsComponent("clock", typeof(ClockSettings), GenerateLifecycle = false)]
            public sealed class ClockViewModel { }

            [SettingsComponent("logRender", typeof(LogRenderSettings), GenerateLifecycle = false)]
            public sealed class LogRenderViewModel { }
            """);

        Assert.Empty(diagnostics);

        var initSource = generatedSources.Values
            .FirstOrDefault(s => s.Contains("GeneratedComponentSettingsInitializer"));
        Assert.NotNull(initSource);
        Assert.Contains("_store.Register<global::TestNs.ClockSettings>(", initSource);
        Assert.Contains(".Child(\"clock\")", initSource);
        Assert.Contains("_store.Register<global::TestNs.LogRenderSettings>(", initSource);
        Assert.Contains(".Child(\"logRender\")", initSource);
        Assert.Contains("_store.CompleteRegistration(resetToDefaults);", initSource);
    }

    [Fact]
    public void Registration_path_uses_root_and_child_names()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsRoot("gui")]
            public sealed partial class Root
            {
                [SettingsChild("appearance")]
                public ParentViewModel App { get; private set; } = null!;
            }

            [SettingsComponent("appearance")]
            public sealed partial class ParentViewModel
            {
                [SettingsChild("leaf")]
                public LeafViewModel Leaf { get; private set; } = null!;
            }

            [SettingsComponent("leaf", typeof(MySettings), GenerateLifecycle = false)]
            public sealed class LeafViewModel { }
            """);

        Assert.Empty(diagnostics);

        var initSource = generatedSources.Values
            .First(s => s.Contains("GeneratedComponentSettingsInitializer"));
        Assert.Contains(".Root(\"gui\").Child(\"appearance\").Child(\"leaf\")", initSource);
    }

    [Fact]
    public void Component_without_settings_type_is_not_registered()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            [SettingsRoot("root")]
            public sealed partial class Root
            {
                [SettingsChild("noSettings")]
                public NoSettingsViewModel Child { get; private set; } = null!;
            }

            [SettingsComponent("noSettings")]
            public sealed class NoSettingsViewModel { }
            """);

        Assert.Empty(diagnostics);

        var hasInit = generatedSources.Values
            .Any(s => s.Contains("GeneratedComponentSettingsInitializer"));
        Assert.False(hasInit, "Should not generate initializer when no leaf has settings type");
    }

    [Fact]
    public void Reports_error_for_invalid_root_name()
    {
        var (diagnostics, _) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsRoot("bad/name")]
            public sealed partial class Root
            {
                [SettingsChild("leaf")]
                public LeafViewModel Leaf { get; private set; } = null!;
            }

            [SettingsComponent("leaf", typeof(MySettings), GenerateLifecycle = false)]
            public sealed class LeafViewModel { }
            """);

        Assert.Contains(diagnostics, d => d.Id == "CSP008");
    }

    [Fact]
    public void Reports_error_for_duplicate_registration_paths()
    {
        var (diagnostics, _) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class SettingsA { }
            public sealed class SettingsB { }

            [SettingsRoot("gui")]
            public sealed partial class Root
            {
                [SettingsChild("same")]
                public ChildAViewModel ChildA { get; private set; } = null!;

                [SettingsChild("same")]
                public ChildBViewModel ChildB { get; private set; } = null!;
            }

            [SettingsComponent("same", typeof(SettingsA), GenerateLifecycle = false)]
            public sealed class ChildAViewModel { }

            [SettingsComponent("same", typeof(SettingsB), GenerateLifecycle = false)]
            public sealed class ChildBViewModel { }
            """);

        Assert.Contains(diagnostics, d => d.Id == "CSP009");
    }

    [Fact]
    public void Multiple_roots_produce_registrations_for_all_trees()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class SettingsA { }
            public sealed class SettingsB { }

            [SettingsRoot("rootA")]
            public sealed partial class RootA
            {
                [SettingsChild("leaf")]
                public LeafAViewModel Leaf { get; private set; } = null!;
            }

            [SettingsRoot("rootB")]
            public sealed partial class RootB
            {
                [SettingsChild("leaf")]
                public LeafBViewModel Leaf { get; private set; } = null!;
            }

            [SettingsComponent("leaf", typeof(SettingsA), GenerateLifecycle = false)]
            public sealed class LeafAViewModel { }

            [SettingsComponent("leaf", typeof(SettingsB), GenerateLifecycle = false)]
            public sealed class LeafBViewModel { }
            """);

        Assert.Empty(diagnostics);

        var initSource = generatedSources.Values
            .First(s => s.Contains("GeneratedComponentSettingsInitializer"));
        Assert.Contains(".Root(\"rootA\").Child(\"leaf\")", initSource);
        Assert.Contains(".Root(\"rootB\").Child(\"leaf\")", initSource);
    }
}
