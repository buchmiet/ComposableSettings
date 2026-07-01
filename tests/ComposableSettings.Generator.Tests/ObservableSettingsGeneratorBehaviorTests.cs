using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public class ObservableSettingsGeneratorBehaviorTests(ITestOutputHelper output) : GeneratorBaseClass(output)
{
    private const string ClockSettingsSource = """
        using ComposableSettings.Attributes;

        namespace Demo.Behavior;

        [SettingsModel]
        public partial class ClockSettings
        {
            private bool _isGlslEnabled = true;
            private string _baseColor = "#e6194b";
        }
        """;

    private const string ClockViewModelWithProxySource = """
        using ComposableSettings.Attributes;
        using ComposableSettings.Observable;

        namespace Demo.Behavior;

        [SettingsVm(typeof(ClockSettings))]
        public partial class ClockViewModel : ObservableObjectStub
        {
            public ClockViewModel(ISettingsProvider<ClockSettings> provider) => InitializeSettings(provider);

            [SettingsProxy] public partial bool IsGlslEnabled { get; set; }
        }
        """;

    [Fact]
    public void Proxy_property_set_raises_vm_PropertyChanged_for_proxy_name()
    {
        const string runnerSource = """
            using System.Collections.Generic;
            using System.Linq;
            using ComposableSettings.Testing;

            namespace Demo.Behavior;

            public static class ProxyPropertyRaiseTest
            {
                public static string[] Run()
                {
                    var provider = new TestSettingsProvider<ClockSettings>();
                    var vm = new ClockViewModel(provider);
                    var names = new List<string>();
                    vm.PropertyChanged += (_, e) => names.Add(e.PropertyName ?? string.Empty);
                    vm.IsGlslEnabled = false;
                    return names.ToArray();
                }
            }
            """;

        var names = (string[])CompileEmitAndInvokeObservableSettings(
            "Demo.Behavior.ProxyPropertyRaiseTest",
            userSources: [ClockSettingsSource, ClockViewModelWithProxySource, runnerSource]);

        Assert.Contains("IsGlslEnabled", names);
    }

    [Fact]
    public void Manual_BaseColor_projection_receives_PropertyChanged_when_settings_member_changes()
    {
        const string viewModelSource = """
            using ComposableSettings.Attributes;
            using ComposableSettings.Observable;

            namespace Demo.Behavior;

            [SettingsVm(typeof(ClockSettings))]
            public partial class ClockViewModel : ObservableObjectStub
            {
                public ClockViewModel(ISettingsProvider<ClockSettings> provider) => InitializeSettings(provider);

                public string BaseColor => Settings.BaseColor;
            }
            """;

        const string runnerSource = """
            using System.Collections.Generic;
            using System.Linq;
            using ComposableSettings.Testing;

            namespace Demo.Behavior;

            public static class ManualBaseColorProjectionTest
            {
                public static string[] Run()
                {
                    var provider = new TestSettingsProvider<ClockSettings>();
                    var vm = new ClockViewModel(provider);
                    var names = new List<string>();
                    vm.PropertyChanged += (_, e) => names.Add(e.PropertyName ?? string.Empty);
                    vm.Settings.BaseColor = "#3366cc";
                    return names.ToArray();
                }
            }
            """;

        var names = (string[])CompileEmitAndInvokeObservableSettings(
            "Demo.Behavior.ManualBaseColorProjectionTest",
            userSources: [ClockSettingsSource, viewModelSource, runnerSource]);

        Assert.Contains("BaseColor", names);
    }

    [Fact]
    public void OnSettingsMemberChanged_receives_settings_property_name()
    {
        const string viewModelSource = """
            using System.Collections.Generic;
            using ComposableSettings.Attributes;
            using ComposableSettings.Observable;

            namespace Demo.Behavior;

            [SettingsVm(typeof(ClockSettings))]
            public partial class ClockViewModel : ObservableObjectStub
            {
                public readonly List<string?> MemberChangeCalls = new();

                public ClockViewModel(ISettingsProvider<ClockSettings> provider) => InitializeSettings(provider);

                partial void OnSettingsMemberChanged(string? propertyName)
                    => MemberChangeCalls.Add(propertyName);
            }
            """;

        const string runnerSource = """
            using ComposableSettings.Testing;

            namespace Demo.Behavior;

            public static class OnSettingsMemberChangedTest
            {
                public static string?[] Run()
                {
                    var provider = new TestSettingsProvider<ClockSettings>();
                    var vm = new ClockViewModel(provider);
                    vm.Settings.IsGlslEnabled = false;
                    return vm.MemberChangeCalls.ToArray();
                }
            }
            """;

        var hookCalls = (string?[])CompileEmitAndInvokeObservableSettings(
            "Demo.Behavior.OnSettingsMemberChangedTest",
            userSources: [ClockSettingsSource, viewModelSource, runnerSource]);

        Assert.Contains("IsGlslEnabled", hookCalls);
    }

    [Fact]
    public void Provider_Reset_rehooks_and_new_instance_PropertyChanged_reaches_vm()
    {
        const string runnerSource = """
            using System.Collections.Generic;
            using System.Linq;
            using ComposableSettings.Testing;

            namespace Demo.Behavior;

            public static class ProviderReplacedRehooksTest
            {
                public static (bool ReplacedWithNewInstance, string[] PropertyNames) Run()
                {
                    var provider = new TestSettingsProvider<ClockSettings>();
                    var vm = new ClockViewModel(provider);
                    var before = provider.Current;
                    provider.Reset();
                    var after = provider.Current;
                    var names = new List<string>();
                    vm.PropertyChanged += (_, e) => names.Add(e.PropertyName ?? string.Empty);
                    vm.IsGlslEnabled = false;
                    return (before != after, names.ToArray());
                }
            }
            """;

        var result = ((bool ReplacedWithNewInstance, string[] PropertyNames))CompileEmitAndInvokeObservableSettings(
            "Demo.Behavior.ProviderReplacedRehooksTest",
            userSources: [ClockSettingsSource, ClockViewModelWithProxySource, runnerSource]);

        Assert.True(result.ReplacedWithNewInstance);
        Assert.Contains("IsGlslEnabled", result.PropertyNames);
    }

    [Fact]
    public void Old_settings_instance_after_provider_replace_does_not_raise_vm_PropertyChanged()
    {
        const string runnerSource = """
            using System.Collections.Generic;
            using System.Linq;
            using ComposableSettings.Testing;

            namespace Demo.Behavior;

            public static class OldInstanceAfterReplaceTest
            {
                public static string[] Run()
                {
                    var provider = new TestSettingsProvider<ClockSettings>();
                    var vm = new ClockViewModel(provider);
                    var oldSettings = provider.Current;
                    var names = new List<string>();
                    vm.PropertyChanged += (_, e) => names.Add(e.PropertyName ?? string.Empty);
                    provider.Reset();
                    names.Clear();
                    oldSettings.IsGlslEnabled = !oldSettings.IsGlslEnabled;
                    return names.ToArray();
                }
            }
            """;

        var names = (string[])CompileEmitAndInvokeObservableSettings(
            "Demo.Behavior.OldInstanceAfterReplaceTest",
            userSources: [ClockSettingsSource, ClockViewModelWithProxySource, runnerSource]);

        Assert.DoesNotContain("IsGlslEnabled", names);
    }

    [Fact]
    public void Old_settings_instance_after_DisposeGeneratedSettings_does_not_raise_vm_PropertyChanged()
    {
        const string runnerSource = """
            using System.Collections.Generic;
            using System.Linq;
            using ComposableSettings.Testing;

            namespace Demo.Behavior;

            public static class OldInstanceAfterDisposeTest
            {
                public static string[] Run()
                {
                    var provider = new TestSettingsProvider<ClockSettings>();
                    var vm = new ClockViewModel(provider);
                    var settings = provider.Current;
                    var names = new List<string>();
                    vm.PropertyChanged += (_, e) => names.Add(e.PropertyName ?? string.Empty);
                    vm.DisposeGeneratedSettings();
                    names.Clear();
                    settings.IsGlslEnabled = !settings.IsGlslEnabled;
                    provider.Current.IsGlslEnabled = !provider.Current.IsGlslEnabled;
                    return names.ToArray();
                }
            }
            """;

        var names = (string[])CompileEmitAndInvokeObservableSettings(
            "Demo.Behavior.OldInstanceAfterDisposeTest",
            userSources: [ClockSettingsSource, ClockViewModelWithProxySource, runnerSource]);

        Assert.Empty(names);
    }

    [Fact]
    public void Reports_CSP032_when_settings_proxy_has_no_matching_settings_property()
    {
        const string source = """
            using ComposableSettings.Attributes;

            namespace Demo;

            [SettingsModel]
            public partial class ClockSettings { private bool _isGlslEnabled = true; }

            [SettingsVm(typeof(ClockSettings))]
            public partial class ClockViewModel : ObservableObjectStub
            {
                [SettingsProxy] public partial int MissingOnModel { get; set; }
            }
            """;

        var (diagnostics, _) = CompileAndRunObservableSettingsGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP032");
    }

    [Fact]
    public void Reports_CSP033_when_settings_proxy_type_does_not_match_settings_property()
    {
        const string source = """
            using ComposableSettings.Attributes;

            namespace Demo;

            [SettingsModel]
            public partial class ClockSettings { private bool _isGlslEnabled = true; }

            [SettingsVm(typeof(ClockSettings))]
            public partial class ClockViewModel : ObservableObjectStub
            {
                [SettingsProxy] public partial string IsGlslEnabled { get; set; }
            }
            """;

        var (diagnostics, _) = CompileAndRunObservableSettingsGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "CSP033");
    }
}
