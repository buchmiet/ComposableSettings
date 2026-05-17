using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public sealed class SettingsLifecycleGeneratorTests(ITestOutputHelper output) : GeneratorBaseClass(output)
{
    [Fact]
    public void Lifecycle_generated_by_default_when_settings_type_declared()
    {
        // Opt-out: a settings type and no GenerateLifecycle argument => lifecycle.
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsComponent("x", typeof(MySettings))]
            public partial class MyComponent { }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        Assert.True(generatedSources.Values.Any(s => s.Contains("ResetSettingsAsync")),
            "Lifecycle is opt-out: it must generate by default when a settings type is declared");
    }

    [Fact]
    public void Explicit_opt_out_skips_lifecycle()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsComponent("x", typeof(MySettings), GenerateLifecycle = false)]
            public partial class MyComponent { }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        Assert.False(generatedSources.Values.Any(s => s.Contains("ResetSettingsAsync")),
            "GenerateLifecycle = false must disable lifecycle generation");
    }

    [Fact]
    public void Grouping_node_without_settings_type_skips_lifecycle_silently()
    {
        // No settings type and no explicit flag => pure tree/grouping node.
        // Must NOT generate lifecycle and must NOT raise CSP012.
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            [SettingsComponent("appearance")]
            public partial class AppearanceComponent { }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CSP012");
        Assert.False(generatedSources.Values.Any(s => s.Contains("ResetSettingsAsync")),
            "A grouping node without a settings type must not get a lifecycle");
    }

    [Fact]
    public void Lifecycle_opt_in_generates_all_members()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsComponent("x", typeof(MySettings), GenerateLifecycle = true)]
            public partial class MyComponent { }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));

        var lifecycleSource = generatedSources.Values
            .FirstOrDefault(s => s.Contains("ResetSettingsAsync"));
        Assert.NotNull(lifecycleSource);

        Assert.Contains("public global::TestNs.MySettings Settings", lifecycleSource);
        Assert.Contains("public async Task ResetSettingsAsync", lifecycleSource);
        Assert.Contains("public async Task SaveSettingsAsync", lifecycleSource);
        Assert.Contains("protected virtual Task SettingsUpdatedAsync", lifecycleSource);
        Assert.Contains("private readonly global::ComposableSettings.IComponentSettings<global::TestNs.MySettings> _componentSettings", lifecycleSource);
    }

    [Fact]
    public void Reset_behavior_creates_new_instance_and_calls_updated()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsComponent("x", typeof(MySettings), GenerateLifecycle = true)]
            public partial class MyComponent { }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));

        var source = generatedSources.Values.First(s => s.Contains("ResetSettingsAsync"));
        Assert.Contains("Settings = new global::TestNs.MySettings();", source);
        Assert.Contains("await SettingsUpdatedAsync(Settings, cancellationToken);", source);
    }

    [Fact]
    public void Save_behavior_calls_save_on_component_settings()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsComponent("x", typeof(MySettings), GenerateLifecycle = true)]
            public partial class MyComponent { }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));

        var source = generatedSources.Values.First(s => s.Contains("SaveSettingsAsync"));
        Assert.Contains("await _componentSettings.SaveAsync(Settings, cancellationToken);", source);
        // SaveSettingsAsync body should NOT call SettingsUpdatedAsync
        var saveStart = source.IndexOf("public async Task SaveSettingsAsync");
        var nextMethodStart = source.IndexOf("protected virtual Task SettingsUpdatedAsync", saveStart);
        var saveBody = source.Substring(saveStart, nextMethodStart - saveStart);
        Assert.DoesNotContain("SettingsUpdatedAsync(", saveBody);
    }

    [Fact]
    public void SettingsUpdatedAsync_is_protected_virtual()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsComponent("x", typeof(MySettings), GenerateLifecycle = true)]
            public partial class MyComponent { }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));

        var source = generatedSources.Values.First(s => s.Contains("SettingsUpdatedAsync"));
        Assert.Contains("protected virtual Task SettingsUpdatedAsync", source);
        Assert.Contains("return Task.CompletedTask;", source);
    }

    [Fact]
    public void Non_partial_class_produces_diagnostic()
    {
        var (diagnostics, _) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsComponent("x", typeof(MySettings), GenerateLifecycle = true)]
            public class MyComponent { }
            """);

        Assert.Contains(diagnostics, d => d.Id == "CSP011");
    }

    [Fact]
    public void Missing_settings_type_produces_diagnostic()
    {
        var (diagnostics, _) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            [SettingsComponent("x", GenerateLifecycle = true)]
            public partial class MyComponent { }
            """);

        Assert.Contains(diagnostics, d => d.Id == "CSP012");
    }

    [Fact]
    public void Settings_type_without_parameterless_ctor_produces_diagnostic()
    {
        var (diagnostics, _) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings
            {
                public MySettings(string required) { }
            }

            [SettingsComponent("x", typeof(MySettings), GenerateLifecycle = true)]
            public partial class MyComponent { }
            """);

        Assert.Contains(diagnostics, d => d.Id == "CSP013");
    }

    [Fact]
    public void Existing_member_produces_diagnostic()
    {
        var (diagnostics, _) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsComponent("x", typeof(MySettings), GenerateLifecycle = true)]
            public partial class MyComponent
            {
                public MySettings Settings { get; set; } = new();
            }
            """);

        Assert.Contains(diagnostics, d => d.Id == "CSP014");
    }

    [Fact]
    public void Generates_constructor_when_no_user_constructors_exist()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsComponent("x", typeof(MySettings), GenerateLifecycle = true)]
            public partial class MyComponent { }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));

        var source = generatedSources.Values.First(s => s.Contains("ResetSettingsAsync"));
        Assert.Contains("public MyComponent(global::ComposableSettings.IComponentSettings<global::TestNs.MySettings> componentSettings)", source);
        Assert.Contains("_componentSettings = componentSettings;", source);
        Assert.Contains("Settings = new global::TestNs.MySettings();", source);
    }

    [Fact]
    public void User_defined_constructor_reports_diagnostic_and_skips_lifecycle()
    {
        var (diagnostics, generatedSources) = CompileAndRunAllGenerators("""
            using ComposableSettings;

            namespace TestNs;

            public sealed class MySettings { }

            [SettingsComponent("x", typeof(MySettings), GenerateLifecycle = true)]
            public partial class MyComponent
            {
                public MyComponent(int unused) { }
            }
            """);

        Assert.Contains(diagnostics, d => d.Id == "CSP015");
        Assert.False(
            generatedSources.Values.Any(s => s.Contains("Lifecycle.g.cs") && s.Contains("MyComponent")),
            "Should not generate lifecycle source when user constructor exists");
    }
}
