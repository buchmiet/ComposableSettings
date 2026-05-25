using ComposableSettings.Runtime;
using ComposableSettings.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Tests;

public sealed class ObservableSettingsTests
{
    public sealed class RuntimeTestSettings : ObservableSettings
    {
        private string _pluginsFolder = "./plugins";
        private int _maxConcurrentRuns = 2;

        public string PluginsFolder { get => _pluginsFolder; set => SetProperty(ref _pluginsFolder, value); }
        public int MaxConcurrentRuns { get => _maxConcurrentRuns; set => SetProperty(ref _maxConcurrentRuns, value); }
    }

    public sealed class GuiTestSettings : ObservableSettings
    {
        private double _brightness = 0.8;
        public double Brightness { get => _brightness; set => SetProperty(ref _brightness, value); }
    }

    private static (ServiceProvider Sp, string RuntimeFile, string GuiFile) BuildTwoFileSetup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ComposableSettingsTests", Guid.NewGuid().ToString("N"));
        var runtimeFile = Path.Combine(dir, "runtime.xml");
        var guiFile = Path.Combine(dir, "gui.xml");

        var services = new ServiceCollection();
        // Two owners, two files — registered independently, no shared knowledge.
        services.AddComposableSettingsFile("runtime", runtimeFile);
        services.AddComposableSettingsFile("gui", guiFile);
        services.AddSettingsProvider<RuntimeTestSettings>("runtime", SettingsNodePath.Root("runtime"));
        services.AddSettingsProvider<GuiTestSettings>("gui", SettingsNodePath.Root("clock"));

        return (services.BuildServiceProvider(), runtimeFile, guiFile);
    }

    [Fact]
    public void Provider_exposes_defaults_when_file_is_empty()
    {
        var (sp, _, _) = BuildTwoFileSetup();
        using (sp)
        {
            var runtime = sp.GetRequiredService<ISettingsProvider<RuntimeTestSettings>>();
            Assert.Equal(2, runtime.Current.MaxConcurrentRuns);
            Assert.Equal("./plugins", runtime.Current.PluginsFolder);
        }
    }

    [Fact]
    public void Mutating_current_autopersists_without_explicit_save()
    {
        var (sp, runtimeFile, _) = BuildTwoFileSetup();
        using (sp)
        {
            var runtime = sp.GetRequiredService<ISettingsProvider<RuntimeTestSettings>>();
            runtime.Current.MaxConcurrentRuns = 8;   // no Save() — provider auto-persists on change
        }

        // Re-open the file from scratch: the value survived to disk.
        var reopened = new XmlSettingsFile(runtimeFile).Get<RuntimeTestSettings>(SettingsNodePath.Root("runtime"));
        Assert.Equal(8, reopened.MaxConcurrentRuns);
    }

    [Fact]
    public void Runtime_and_gui_settings_live_in_separate_files()
    {
        var (sp, runtimeFile, guiFile) = BuildTwoFileSetup();
        using (sp)
        {
            sp.GetRequiredService<ISettingsProvider<RuntimeTestSettings>>().Current.MaxConcurrentRuns = 9;
            sp.GetRequiredService<ISettingsProvider<GuiTestSettings>>().Current.Brightness = 0.25;
        }

        // The runtime change is in runtime.xml only; the gui file knows nothing about it.
        Assert.Equal(9, new XmlSettingsFile(runtimeFile).Get<RuntimeTestSettings>(SettingsNodePath.Root("runtime")).MaxConcurrentRuns);
        Assert.Equal(0.25, new XmlSettingsFile(guiFile).Get<GuiTestSettings>(SettingsNodePath.Root("clock")).Brightness);

        // Cross-read returns defaults: the files are independent (runtime knows nothing of gui and vice versa).
        Assert.Equal(2, new XmlSettingsFile(guiFile).Get<RuntimeTestSettings>(SettingsNodePath.Root("runtime")).MaxConcurrentRuns);
        Assert.Equal(0.8, new XmlSettingsFile(runtimeFile).Get<GuiTestSettings>(SettingsNodePath.Root("clock")).Brightness);
    }

    [Fact]
    public void Reset_restores_defaults_and_raises_Replaced()
    {
        var (sp, _, _) = BuildTwoFileSetup();
        using (sp)
        {
            var runtime = sp.GetRequiredService<ISettingsProvider<RuntimeTestSettings>>();
            runtime.Current.MaxConcurrentRuns = 7;

            var replacedRaised = false;
            runtime.Replaced += (_, _) => replacedRaised = true;

            runtime.Reset();

            Assert.True(replacedRaised);
            Assert.Equal(2, runtime.Current.MaxConcurrentRuns);
        }
    }
}
