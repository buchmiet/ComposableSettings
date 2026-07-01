using ComposableSettings.Document;
using ComposableSettings.Layering;
using ComposableSettings.Packs;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Tests;

public  class DocumentLayeringTests
{
    public  class LayerDocument
    {
        public string ThemeId { get; set; } = "default";
        public int WindowWidth { get; set; } = 1024;
        public NestedSection Layout { get; set; } = new();
    }

    public  class NestedSection
    {
        public double Opacity { get; set; } = 1.0;
    }

    private static JsonSettingsLayerMerger<LayerDocument> CreateMerger()
    {
        var policy = new SettingsMergePolicy();
        policy.MergeableRootProperties.Add("themeId");
        policy.MergeableRootProperties.Add("layout");
        policy.UserOwnedRootProperties.Add("windowWidth");
        policy.UserMergeMode = SettingsMergeMode.DeepMergeNonDefault;
        return new JsonSettingsLayerMerger<LayerDocument>(policy, new JsonSettingsDocumentSerializer<LayerDocument>());
    }

    [Fact]
    public void LayerMerger_applies_pack_then_user_with_owned_root_properties()
    {
        var merger = CreateMerger();
        var defaults = new LayerDocument
        {
            ThemeId = "factory",
            WindowWidth = 1024,
            Layout = new NestedSection { Opacity = 0.5 },
        };
        var pack = new LayerDocument
        {
            ThemeId = "ocean",
            WindowWidth = 640,
            Layout = new NestedSection { Opacity = 0.2 },
        };
        var user = new LayerDocument
        {
            ThemeId = "nord",
            WindowWidth = 800,
            Layout = new NestedSection { Opacity = 0.9 },
        };

        var merged = merger.Merge(defaults, pack, user);

        Assert.Equal("nord", merged.ThemeId);
        Assert.Equal(800, merged.WindowWidth);
        Assert.Equal(0.9, merged.Layout.Opacity);
    }

    [Fact]
    public void LayerMerger_skips_user_values_equal_to_factory_defaults()
    {
        var merger = CreateMerger();
        var defaults = new LayerDocument { ThemeId = "factory", Layout = new NestedSection { Opacity = 0.5 } };
        var user = new LayerDocument { ThemeId = "default", Layout = new NestedSection { Opacity = 0.5 } };

        var merged = merger.Merge(defaults, packOverlay: null, user);

        Assert.Equal("factory", merged.ThemeId);
        Assert.Equal(0.5, merged.Layout.Opacity);
    }

    [Fact]
    public void DI_layering_wires_into_document_store()
    {
        var path = CreateTempFilePath();
        var services = new ServiceCollection();
        services.AddComposableSettingsDocument<LayerDocument>(o =>
        {
            o.FilePath = path;
            o.DefaultsFactory = () => new LayerDocument { ThemeId = "factory", Layout = new NestedSection { Opacity = 0.5 } };
            o.AutosaveDelay = TimeSpan.FromMilliseconds(25);
        });
        services.AddComposableSettingsLayering<LayerDocument>(policy =>
        {
            policy.MergeableRootProperties.Add("themeId");
            policy.MergeableRootProperties.Add("layout");
            policy.UserOwnedRootProperties.Add("windowWidth");
            policy.UserMergeMode = SettingsMergeMode.DeepMergeNonDefault;
        });

        var store = services.BuildServiceProvider().GetRequiredService<ISettingsDocumentStore<LayerDocument>>();
        store.Preview(new LayerDocument
        {
            ThemeId = "nord",
            WindowWidth = 640,
            Layout = new NestedSection { Opacity = 0.75 },
        });

        Assert.Equal("nord", store.Effective.ThemeId);
        Assert.Equal(640, store.Effective.WindowWidth);
        Assert.Equal(0.75, store.Effective.Layout.Opacity);
    }

    private static string CreateTempFilePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ComposableSettings.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }
}
