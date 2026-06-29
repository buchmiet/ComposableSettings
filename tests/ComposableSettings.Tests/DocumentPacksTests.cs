using ComposableSettings.Document;
using ComposableSettings.Packs;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Tests;

public sealed class DocumentPacksTests
{
    public sealed class PackDocument
    {
        public string ThemeId { get; set; } = "default";
        public string PackId { get; set; } = "";
        public int PanelWidth { get; set; }
        public NestedSection Layout { get; set; } = new();
    }

    public sealed class NestedSection
    {
        public double Opacity { get; set; } = 1.0;
    }

    [Fact]
    public async Task PackLoader_extracts_zip_and_reads_overlay()
    {
        var root = CreateTempRoot();
        var packsDir = Path.Combine(root, "packs");
        var cacheDir = Path.Combine(root, "cache");
        var packPath = Path.Combine(packsDir, "ocean.settingspack");
        Directory.CreateDirectory(packsDir);

        var exporter = new SettingsPackExporter<PackDocument>();
        await exporter.ExportAsync(
            packPath,
            new PackDocument { ThemeId = "ocean", Layout = new NestedSection { Opacity = 0.25 } },
            new SettingsPackManifest { Id = "ocean", Name = "Ocean", Version = "1.0.0" });

        var loader = new SettingsPackLoader(new SettingsPackOptions
        {
            PacksDirectory = packsDir,
            CacheDirectory = cacheDir,
            Extension = ".settingspack",
        });

        var result = await loader.LoadAsync(packPath);
        Assert.NotNull(result);
        Assert.Equal("ocean", result!.Manifest.Id);
        Assert.Contains("ocean", result.OverlayJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Document_store_applies_installed_pack_to_effective()
    {
        var root = CreateTempRoot();
        var path = Path.Combine(root, "settings.json");
        var packsDir = Path.Combine(root, "packs");
        var cacheDir = Path.Combine(root, "cache");
        Directory.CreateDirectory(packsDir);

        var exporter = new SettingsPackExporter<PackDocument>();
        await exporter.ExportAsync(
            Path.Combine(packsDir, "ocean"),
            new PackDocument { ThemeId = "ocean", Layout = new NestedSection { Opacity = 0.2 } },
            new SettingsPackManifest { Id = "ocean", Name = "Ocean", Version = "1.0.0" });

        var services = new ServiceCollection();
        services.AddComposableSettingsDocument<PackDocument>(o =>
        {
            o.FilePath = path;
            o.DefaultsFactory = () => new PackDocument
            {
                ThemeId = "factory",
                PanelWidth = 80,
                Layout = new NestedSection { Opacity = 0.5 },
            };
            o.AutosaveDelay = TimeSpan.FromMilliseconds(25);
        });
        services.AddComposableSettingsPacks<PackDocument>(o =>
        {
            o.PacksDirectory = packsDir;
            o.CacheDirectory = cacheDir;
            o.Extension = ".settingspack";
        }, doc => doc.PackId);

        var store = services.BuildServiceProvider().GetRequiredService<ISettingsDocumentStore<PackDocument>>();
        store.Preview(new PackDocument { PackId = "ocean", PanelWidth = 60 });

        Assert.Equal("ocean", store.Effective.ThemeId);
        Assert.Equal(60, store.Effective.PanelWidth);
        Assert.Equal(0.2, store.Effective.Layout.Opacity);
    }

    [Fact]
    public async Task PackExporter_writes_installable_zip()
    {
        var root = CreateTempRoot();
        var packsDir = Path.Combine(root, "packs");
        Directory.CreateDirectory(packsDir);
        var output = Path.Combine(packsDir, "exported.settingspack");

        var exporter = new SettingsPackExporter<PackDocument>();
        await exporter.ExportAsync(
            output,
            new PackDocument { ThemeId = "exported", Layout = new NestedSection { Opacity = 0.33 } },
            new SettingsPackManifest { Id = "exported", Name = "Exported", Version = "1.0.0" });

        Assert.True(File.Exists(output));

        var catalog = new SettingsPackCatalog<PackDocument>(
            new SettingsPackOptions
            {
                PacksDirectory = packsDir,
                CacheDirectory = Path.Combine(root, "cache"),
                Extension = ".settingspack",
            },
            new SettingsPackLoader(new SettingsPackOptions
            {
                PacksDirectory = packsDir,
                CacheDirectory = Path.Combine(root, "cache"),
                Extension = ".settingspack",
            }),
            new JsonSettingsDocumentSerializer<PackDocument>());

        var overlay = catalog.TryLoadOverlay("exported", new PackDocument());
        Assert.NotNull(overlay);
        Assert.Equal("exported", overlay!.ThemeId);
        Assert.Equal(0.33, overlay.Layout.Opacity);
    }

    [Fact]
    public async Task PackCatalog_lists_installed_packs()
    {
        var root = CreateTempRoot();
        var packsDir = Path.Combine(root, "packs");
        Directory.CreateDirectory(packsDir);

        var exporter = new SettingsPackExporter<PackDocument>();
        await exporter.ExportAsync(
            Path.Combine(packsDir, "alpha"),
            new PackDocument { ThemeId = "alpha" },
            new SettingsPackManifest { Id = "alpha", Name = "Alpha", Version = "1.0.0" });

        var catalog = new SettingsPackCatalog<PackDocument>(
            new SettingsPackOptions { PacksDirectory = packsDir, CacheDirectory = Path.Combine(root, "cache") },
            new SettingsPackLoader(new SettingsPackOptions { PacksDirectory = packsDir, CacheDirectory = Path.Combine(root, "cache") }),
            new JsonSettingsDocumentSerializer<PackDocument>());

        var installed = catalog.ListInstalled();
        Assert.Single(installed);
        Assert.Equal("alpha", installed[0].Id);
    }

    private static string CreateTempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ComposableSettings.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
