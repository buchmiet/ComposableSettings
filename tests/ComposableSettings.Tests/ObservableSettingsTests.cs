using System.Collections.ObjectModel;
using ComposableSettings.Observable;
using ComposableSettings.Runtime;
using ComposableSettings.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Tests;

public  class ObservableSettingsTests
{
    private  class CountingComponentSettingsProvider : IComponentSettingsProvider
    {
        private readonly object _gate = new();
        private object? _lastValue;

        public int SetCount { get; private set; }

        public TSettings Get<TSettings>(SettingsNodePath path)
            where TSettings : class, new()
        {
            lock (_gate)
            {
                return _lastValue is TSettings typed ? typed : new TSettings();
            }
        }

        public void Set<TSettings>(SettingsNodePath path, TSettings value)
            where TSettings : class, new()
        {
            lock (_gate)
            {
                SetCount++;
                _lastValue = value;
            }
        }

        public TSettings? Last<TSettings>()
            where TSettings : class
        {
            lock (_gate)
            {
                return _lastValue as TSettings;
            }
        }
    }

    public  class RuntimeTestSettings : ObservableSettings
    {
        private string _pluginsFolder = "./plugins";
        private int _maxConcurrentRuns = 2;

        public string PluginsFolder { get => _pluginsFolder; set => SetProperty(ref _pluginsFolder, value); }
        public int MaxConcurrentRuns { get => _maxConcurrentRuns; set => SetProperty(ref _maxConcurrentRuns, value); }
    }

    public  class GuiTestSettings : ObservableSettings
    {
        private double _brightness = 0.8;
        public double Brightness { get => _brightness; set => SetProperty(ref _brightness, value); }
    }

    // Hand-written equivalents of what SettingsModelGenerator emits (1.0.10):
    // collections via SettingsChangeTracking.TrackCollection; nested objects tracked
    // from the ctor with a re-tracking setter.

    public  class PaletteTestSettings : ObservableSettings
    {
        public ObservableCollection<string> Colors { get; } = new() { "#a", "#b" };

        public PaletteTestSettings()
            => SettingsChangeTracking.TrackCollection(Colors, () => RaisePropertyChanged(nameof(Colors)));
    }

    public  class ScheduleItem : ObservableSettings
    {
        private string _jobId = string.Empty;
        private string _cron = string.Empty;

        public string JobId { get => _jobId; set => SetProperty(ref _jobId, value); }
        public string Cron { get => _cron; set => SetProperty(ref _cron, value); }
    }

    public  class SchedulesTestSettings : ObservableSettings
    {
        public ObservableCollection<ScheduleItem> Schedules { get; } = new();

        public SchedulesTestSettings()
            => SettingsChangeTracking.TrackCollection(Schedules, () => RaisePropertyChanged(nameof(Schedules)));
    }

    public  class ClockChildSettings : ObservableSettings
    {
        private double _brightness = 0.8;
        public double Brightness { get => _brightness; set => SetProperty(ref _brightness, value); }
    }

    public  class AppearanceTestSettings : ObservableSettings
    {
        private ClockChildSettings _clock = new();

        public AppearanceTestSettings()
            => SettingsChangeTracking.Track(_clock, Clock__OnChildChanged);

        public ClockChildSettings Clock
        {
            get => _clock;
            set
            {
                if (ReferenceEquals(_clock, value)) return;
                SettingsChangeTracking.Untrack(_clock, Clock__OnChildChanged);
                _clock = value;
                SettingsChangeTracking.Track(_clock, Clock__OnChildChanged);
                RaisePropertyChanged(nameof(Clock));
            }
        }

        private void Clock__OnChildChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            => RaisePropertyChanged(nameof(Clock));
    }

    private static (ServiceProvider Sp, string RuntimeFile, string GuiFile) BuildTwoFileSetup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ComposableSettingsTests", Guid.NewGuid().ToString("N"));
        var runtimeFile = Path.Combine(dir, "runtime.xml");
        var guiFile = Path.Combine(dir, "gui.xml");

        var services = new ServiceCollection();
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
            runtime.Current.MaxConcurrentRuns = 8;
        }

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

        Assert.Equal(9, new XmlSettingsFile(runtimeFile).Get<RuntimeTestSettings>(SettingsNodePath.Root("runtime")).MaxConcurrentRuns);
        Assert.Equal(0.25, new XmlSettingsFile(guiFile).Get<GuiTestSettings>(SettingsNodePath.Root("clock")).Brightness);

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
        Assert.Equal(new[] { "#a", "#b" }, palette.Current.Colors);

        palette.Current.Colors.Add("#c");

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

        var reopened = new XmlSettingsFile(file).Get<SchedulesTestSettings>(SettingsNodePath.Root("runtime"));
        Assert.Equal(2, reopened.Schedules.Count);
        Assert.Equal("job-a", reopened.Schedules[0].JobId);
        Assert.Equal("0 0 * * *", reopened.Schedules[0].Cron);
        Assert.Equal("job-b", reopened.Schedules[1].JobId);
        Assert.Equal("*/5 * * * *", reopened.Schedules[1].Cron);
    }

    [Fact]
    public void Nested_object_in_place_edit_autopersists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ComposableSettingsTests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(dir, "gui.xml");

        var services = new ServiceCollection();
        services.AddComposableSettingsFile("gui", file);
        services.AddSettingsProvider<AppearanceTestSettings>("gui", SettingsNodePath.Root("appearance"));
        using var sp = services.BuildServiceProvider();

        var appearance = sp.GetRequiredService<ISettingsProvider<AppearanceTestSettings>>();
        appearance.Current.Clock.Brightness = 0.33;   // edit NESTED child in place -> must auto-persist

        var reopened = new XmlSettingsFile(file).Get<AppearanceTestSettings>(SettingsNodePath.Root("appearance"));
        Assert.Equal(0.33, reopened.Clock.Brightness);
    }

    [Fact]
    public void Collection_item_in_place_edit_autopersists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ComposableSettingsTests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(dir, "runtime.xml");

        var services = new ServiceCollection();
        services.AddComposableSettingsFile("runtime", file);
        services.AddSettingsProvider<SchedulesTestSettings>("runtime", SettingsNodePath.Root("runtime"));
        using var sp = services.BuildServiceProvider();

        var schedules = sp.GetRequiredService<ISettingsProvider<SchedulesTestSettings>>();
        var item = new ScheduleItem { JobId = "job-a", Cron = "0 0 * * *" };
        schedules.Current.Schedules.Add(item);   // add -> persist + item now tracked
        item.Cron = "*/10 * * * *";              // edit ITEM in place -> must auto-persist

        var reopened = new XmlSettingsFile(file).Get<SchedulesTestSettings>(SettingsNodePath.Root("runtime"));
        Assert.Equal("*/10 * * * *", reopened.Schedules.Single().Cron);
    }

    [Fact]
    public async Task Debounced_provider_coalesces_persist_writes()
    {
        var store = new CountingComponentSettingsProvider();
        var services = new ServiceCollection();
        services.AddComposableSettingsFile("runtime", store);
        services.AddSettingsProvider<RuntimeTestSettings>(
            "runtime",
            SettingsNodePath.Root("runtime"),
            TimeSpan.FromMilliseconds(50));
        using var sp = services.BuildServiceProvider();

        var runtime = sp.GetRequiredService<ISettingsProvider<RuntimeTestSettings>>();
        runtime.Current.MaxConcurrentRuns = 3;
        runtime.Current.MaxConcurrentRuns = 4;
        runtime.Current.PluginsFolder = "/tmp/plugins";

        Assert.Equal(0, store.SetCount);

        await Task.Delay(200);

        Assert.Equal(1, store.SetCount);
        Assert.Equal(4, store.Last<RuntimeTestSettings>()?.MaxConcurrentRuns);
        Assert.Equal("/tmp/plugins", store.Last<RuntimeTestSettings>()?.PluginsFolder);
    }

    [Fact]
    public void Disposing_debounced_provider_flushes_pending_persist()
    {
        var store = new CountingComponentSettingsProvider();
        var services = new ServiceCollection();
        services.AddComposableSettingsFile("runtime", store);
        services.AddSettingsProvider<RuntimeTestSettings>(
            "runtime",
            SettingsNodePath.Root("runtime"),
            TimeSpan.FromMinutes(1));
        using var sp = services.BuildServiceProvider();

        var runtime = sp.GetRequiredService<ISettingsProvider<RuntimeTestSettings>>();
        runtime.Current.MaxConcurrentRuns = 11;

        sp.Dispose();

        Assert.Equal(1, store.SetCount);
        Assert.Equal(11, store.Last<RuntimeTestSettings>()?.MaxConcurrentRuns);
    }

    [Fact]
    public async Task Reset_cancels_pending_debounced_persist_and_writes_defaults_immediately()
    {
        var store = new CountingComponentSettingsProvider();
        var services = new ServiceCollection();
        services.AddComposableSettingsFile("runtime", store);
        services.AddSettingsProvider<RuntimeTestSettings>(
            "runtime",
            SettingsNodePath.Root("runtime"),
            TimeSpan.FromMilliseconds(50));
        using var sp = services.BuildServiceProvider();

        var runtime = sp.GetRequiredService<ISettingsProvider<RuntimeTestSettings>>();
        runtime.Current.MaxConcurrentRuns = 11;
        runtime.Reset();

        Assert.Equal(1, store.SetCount);
        Assert.Equal(2, store.Last<RuntimeTestSettings>()?.MaxConcurrentRuns);

        await Task.Delay(200);

        Assert.Equal(1, store.SetCount);
        Assert.Equal(2, store.Last<RuntimeTestSettings>()?.MaxConcurrentRuns);
    }
}
