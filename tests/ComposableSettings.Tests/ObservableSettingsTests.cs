using System.Collections.ObjectModel;
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

    // Hand-written equivalent of what SettingsModelGenerator emits for an
    // ObservableCollection field (get-only collection + CollectionChanged -> PropertyChanged).
    public sealed class PaletteTestSettings : ObservableSettings
    {
        public ObservableCollection<string> Colors { get; } = new() { "#a", "#b" };

        public PaletteTestSettings()
            => Colors.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(Colors));
    }

    // Complex collection item (plain POCO; mutate the collection, not the item).
    public sealed class ScheduleItem
    {
        public string JobId { get; set; } = string.Empty;
        public string Cron { get; set; } = string.Empty;
    }

    public sealed class SchedulesTestSettings : ObservableSettings
    {
        public ObservableCollection<ScheduleItem> Schedules { get; } = new();

        public SchedulesTestSettings()
            => Schedules.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(Schedules));
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

    [Fact]
    public void Observable_collection_defaults_persist_and_reload_without_duplication()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ComposableSettingsTests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(dir, "gui.xml");

        var services = new ServiceCollection();
        services.AddComposableSettingsFile("gui", file);
        services.AddSettingsProvider<PaletteTestSettings>("gui", SettingsNodePath.Root("palette"));
        using var sp = services.BuildServiceProvider();

        var palette = sp.GetRequiredService<ISettingsProvider<PaletteTestSettings>>();
        Assert.Equal(new[] { "#a", "#b" }, palette.Current.Colors);   // defaults from field initializer

        palette.Current.Colors.Add("#c");                              // mutation -> auto-persist (no Save)

        // Re-open from disk: exactly a,b,c — defaults replaced, not appended.
        var reopened = new XmlSettingsFile(file).Get<PaletteTestSettings>(SettingsNodePath.Root("palette"));
        Assert.Equal(new[] { "#a", "#b", "#c" }, reopened.Colors);
    }

    [Fact]
    public void Observable_collection_of_complex_items_round_trips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ComposableSettingsTests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(dir, "runtime.xml");

        var services = new ServiceCollection();
        services.AddComposableSettingsFile("runtime", file);
        services.AddSettingsProvider<SchedulesTestSettings>("runtime", SettingsNodePath.Root("runtime"));
        using var sp = services.BuildServiceProvider();

        var schedules = sp.GetRequiredService<ISettingsProvider<SchedulesTestSettings>>();
        Assert.Empty(schedules.Current.Schedules);

        schedules.Current.Schedules.Add(new ScheduleItem { JobId = "job-a", Cron = "0 0 * * *" });
        schedules.Current.Schedules.Add(new ScheduleItem { JobId = "job-b", Cron = "*/5 * * * *" });

        // Re-open from disk: complex items round-trip with their properties intact.
        var reopened = new XmlSettingsFile(file).Get<SchedulesTestSettings>(SettingsNodePath.Root("runtime"));
        Assert.Equal(2, reopened.Schedules.Count);
        Assert.Equal("job-a", reopened.Schedules[0].JobId);
        Assert.Equal("0 0 * * *", reopened.Schedules[0].Cron);
        Assert.Equal("job-b", reopened.Schedules[1].JobId);
        Assert.Equal("*/5 * * * *", reopened.Schedules[1].Cron);
    }
}
