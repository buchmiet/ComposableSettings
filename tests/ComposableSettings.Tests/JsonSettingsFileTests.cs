using System.Collections.ObjectModel;
using ComposableSettings.Runtime;
using ComposableSettings.Stores;

namespace ComposableSettings.Tests;

public  class JsonSettingsFileTests
{
    public  class PaletteTestSettings
    {
        public ObservableCollection<string> Colors { get; } = new() { "#a", "#b" };
        public int MaxColors { get; set; } = 8;
    }

    public  class NestedChildSettings
    {
        public double Brightness { get; set; } = 0.8;
    }

    public  class ParentTestSettings
    {
        public string Name { get; set; } = "default";
        public NestedChildSettings Child { get; set; } = new();
    }

    [Fact]
    public void Round_trips_scalar_settings_at_node_path()
    {
        var file = Path.Combine(Path.GetTempPath(), "ComposableSettings.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var store = new JsonSettingsFile(file);
        var path = SettingsNodePath.Root("clock");
        var original = new ParentTestSettings { Name = "clock-a", Child = new NestedChildSettings { Brightness = 0.42 } };

        store.Set(path, original);

        var reopened = new JsonSettingsFile(file).Get<ParentTestSettings>(path);

        Assert.Equal("clock-a", reopened.Name);
        Assert.Equal(0.42, reopened.Child.Brightness);
    }

    [Fact]
    public void Separate_files_do_not_cross_contaminate_nodes()
    {
        var root = Path.Combine(Path.GetTempPath(), "ComposableSettings.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var guiFile = Path.Combine(root, "gui.json");
        var runtimeFile = Path.Combine(root, "runtime.json");

        var guiStore = new JsonSettingsFile(guiFile);
        var runtimeStore = new JsonSettingsFile(runtimeFile);

        guiStore.Set(SettingsNodePath.Root("clock"), new ParentTestSettings { Name = "gui-clock" });
        runtimeStore.Set(SettingsNodePath.Root("runtime"), new ParentTestSettings { Name = "runtime-root" });

        Assert.Equal("gui-clock", new JsonSettingsFile(guiFile).Get<ParentTestSettings>(SettingsNodePath.Root("clock")).Name);
        Assert.Equal("runtime-root", new JsonSettingsFile(runtimeFile).Get<ParentTestSettings>(SettingsNodePath.Root("runtime")).Name);
        Assert.Equal("default", new JsonSettingsFile(guiFile).Get<ParentTestSettings>(SettingsNodePath.Root("runtime")).Name);
    }

    [Fact]
    public void Missing_node_returns_fresh_defaults()
    {
        var file = Path.Combine(Path.GetTempPath(), "ComposableSettings.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var settings = new JsonSettingsFile(file).Get<ParentTestSettings>(SettingsNodePath.Root("missing"));

        Assert.Equal("default", settings.Name);
        Assert.Equal(0.8, settings.Child.Brightness);
    }

    [Fact]
    public void Corrupt_file_returns_defaults_without_throwing()
    {
        var file = Path.Combine(Path.GetTempPath(), "ComposableSettings.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "{ not valid json");

        var settings = new JsonSettingsFile(file).Get<ParentTestSettings>(SettingsNodePath.Root("clock"));

        Assert.Equal("default", settings.Name);
    }
}
