using Microsoft.CodeAnalysis;
using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public sealed class SettingsChildrenGeneratorTests(ITestOutputHelper output) : GeneratorBaseClass(output)
{
    [Fact]
    public void Generates_child_initialization_for_default_child_name()
    {
        var (_, generatedSources) = CompileAndRunGenerator(BaseRuntimeSource, """
            using ComposableSettings;

            namespace TestNs;

            [SettingsComponent("cX")]
            public sealed class ComponentXViewModel { }

            [SettingsComponent("component-y")]
            public sealed partial class ComponentYViewModel
            {
                [SettingsChild]
                public ComponentXViewModel X { get; private set; } = null!;
            }
            """);

        var generated = Assert.Single(generatedSources.Values);
        Assert.Contains("InitializeGeneratedSettingsChildren", generated);
        Assert.Contains("X = factory.CreateChild<global::TestNs.ComponentXViewModel>(", generated);
        Assert.Contains("parentPath);", generated);
    }

    [Fact]
    public void Generates_child_initialization_for_explicit_child_names()
    {
        var (_, generatedSources) = CompileAndRunGenerator(BaseRuntimeSource, """
            using ComposableSettings;

            namespace TestNs;

            [SettingsComponent("cX")]
            public sealed class ComponentXViewModel { }

            [SettingsComponent("component-with-two-x")]
            public sealed partial class ComponentWithTwoXViewModel
            {
                [SettingsChild("primary-x")]
                public ComponentXViewModel PrimaryX { get; private set; } = null!;

                [SettingsChild("secondary-x")]
                public ComponentXViewModel SecondaryX { get; private set; } = null!;
            }
            """);

        var generated = Assert.Single(generatedSources.Values);
        Assert.Contains("PrimaryX = factory.CreateChild<global::TestNs.ComponentXViewModel>(", generated);
        Assert.Contains("\"primary-x\");", generated);
        Assert.Contains("SecondaryX = factory.CreateChild<global::TestNs.ComponentXViewModel>(", generated);
        Assert.Contains("\"secondary-x\");", generated);
    }

    [Fact]
    public void Reports_error_for_duplicate_default_child_names()
    {
        var (diagnostics, _) = CompileAndRunGenerator(BaseRuntimeSource, """
            using ComposableSettings;

            namespace TestNs;

            [SettingsComponent("cX")]
            public sealed class ComponentXViewModel { }

            [SettingsComponent("bad-parent")]
            public sealed partial class BadParent
            {
                [SettingsChild]
                public ComponentXViewModel First { get; private set; } = null!;

                [SettingsChild]
                public ComponentXViewModel Second { get; private set; } = null!;
            }
            """);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "CSP003"));
        Assert.Contains("cX", diagnostic.GetMessage());
    }

    [Fact]
    public void Allows_same_child_name_under_different_parents()
    {
        var (diagnostics, generatedSources) = CompileAndRunGenerator(BaseRuntimeSource, """
            using ComposableSettings;

            namespace TestNs;

            [SettingsComponent("cX")]
            public sealed class ComponentXViewModel { }

            [SettingsComponent("component-y")]
            public sealed partial class ComponentYViewModel
            {
                [SettingsChild]
                public ComponentXViewModel X { get; private set; } = null!;
            }

            [SettingsComponent("component-z")]
            public sealed partial class ComponentZViewModel
            {
                [SettingsChild]
                public ComponentXViewModel X { get; private set; } = null!;
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Id == "CSP003"));
        Assert.Equal(2, generatedSources.Count);
    }

    [Fact]
    public void Reports_error_when_parent_class_is_not_partial()
    {
        var (diagnostics, _) = CompileAndRunGenerator(BaseRuntimeSource, """
            using ComposableSettings;

            namespace TestNs;

            [SettingsComponent("cX")]
            public sealed class ComponentXViewModel { }

            [SettingsComponent("bad-parent")]
            public sealed class BadParent
            {
                [SettingsChild]
                public ComponentXViewModel X { get; private set; } = null!;
            }
            """);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "CSP001"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Reports_error_for_nested_parent_class()
    {
        var (diagnostics, generatedSources) = CompileAndRunGenerator(BaseRuntimeSource, """
            using ComposableSettings;

            namespace TestNs;

            [SettingsComponent("cX")]
            public sealed class ComponentXViewModel { }

            public static partial class Container
            {
                [SettingsComponent("inner-parent")]
                public sealed partial class InnerParent
                {
                    [SettingsChild]
                    public ComponentXViewModel X { get; private set; } = null!;
                }
            }
            """);

        Assert.Contains(diagnostics, d => d.Id == "CSP006");
        Assert.Empty(generatedSources);
    }

    [Fact]
    public void Reports_error_when_generated_initialization_method_already_exists()
    {
        var (diagnostics, generatedSources) = CompileAndRunGenerator(BaseRuntimeSource, """
            using ComposableSettings;

            namespace TestNs;

            [SettingsComponent("cX")]
            public sealed class ComponentXViewModel { }

            [SettingsComponent("parent")]
            public sealed partial class ParentViewModel
            {
                private void InitializeGeneratedSettingsChildren(
                    ISettingsNodeFactory factory,
                    SettingsNodePath parentPath)
                {
                }

                [SettingsChild]
                public ComponentXViewModel X { get; private set; } = null!;
            }
            """);

        Assert.Contains(diagnostics, d => d.Id == "CSP007");
        Assert.Empty(generatedSources);
    }
}
