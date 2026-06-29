using ComposableSettings.Configuration;
using ComposableSettings.Document;
using ComposableSettings.Static;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Tests;

public sealed class DocumentSettingsStoreTests
{
    public sealed class TestDocument
    {
        public string ThemeId { get; set; } = "default";
        public int PanelWidth { get; set; }
        public NestedSection Layout { get; set; } = new();
    }

    public sealed class NestedSection
    {
        public double Opacity { get; set; } = 1.0;
    }

    private static string CreateTempFilePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ComposableSettings.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

  private static SettingsDocumentStore<TestDocument> CreateStore(
        string filePath,
        TimeSpan? debounce = null)
    {
        return new SettingsDocumentStore<TestDocument>(new SettingsDocumentOptions<TestDocument>
        {
            FilePath = filePath,
            DefaultsFactory = () => new TestDocument
            {
                ThemeId = "factory",
                PanelWidth = 80,
                Layout = new NestedSection { Opacity = 0.5 },
            },
            AutosaveDelay = debounce ?? TimeSpan.FromMilliseconds(50),
            UseAtomicWrites = true,
        });
    }

    [Fact]
    public void Effective_merges_factory_defaults_with_user_layer()
    {
        var path = CreateTempFilePath();
        using var store = CreateStore(path);

        store.Preview(new TestDocument { ThemeId = "nord", PanelWidth = 40 });

        Assert.Equal("nord", store.Effective.ThemeId);
        Assert.Equal(40, store.Effective.PanelWidth);
        Assert.Equal(0.5, store.Effective.Layout.Opacity);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task CommitAsync_persists_user_layer_only_and_reload_restores_effective()
    {
        var path = CreateTempFilePath();
        using var store = CreateStore(path);

        await store.CommitAsync(new TestDocument { ThemeId = "solarized", PanelWidth = 60 });
        await store.FlushAsync();

        Assert.True(File.Exists(path));
        var json = File.ReadAllText(path);
        Assert.Contains("solarized", json, StringComparison.Ordinal);
        Assert.DoesNotContain("factory", json, StringComparison.Ordinal);

        using var reopened = CreateStore(path);
        Assert.Equal("solarized", reopened.Effective.ThemeId);
        Assert.Equal(60, reopened.Effective.PanelWidth);
        Assert.Equal(0.5, reopened.Effective.Layout.Opacity);
    }

    [Fact]
    public async Task Preview_does_not_write_disk_commit_does()
    {
        var path = CreateTempFilePath();
        using var store = CreateStore(path, TimeSpan.FromHours(1));

        store.Preview(new TestDocument { ThemeId = "preview-only" });
        Assert.False(File.Exists(path));

        await store.CommitAsync(new TestDocument { ThemeId = "committed" });
        Assert.False(File.Exists(path));

        await store.FlushAsync();
        Assert.True(File.Exists(path));
        Assert.Contains("committed", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetUserLayerAsync_restores_factory_effective_state()
    {
        var path = CreateTempFilePath();
        using var store = CreateStore(path);

        await store.CommitAsync(new TestDocument { ThemeId = "custom", PanelWidth = 10 });
        await store.FlushAsync();

        await store.ResetUserLayerAsync();

        Assert.Equal("factory", store.Effective.ThemeId);
        Assert.Equal(80, store.Effective.PanelWidth);
    }

    [Fact]
    public void DI_registration_resolves_document_store()
    {
        var path = CreateTempFilePath();
        var services = new ServiceCollection();
        services.AddComposableSettingsDocument<TestDocument>(o =>
        {
            o.FilePath = path;
            o.DefaultsFactory = () => new TestDocument { ThemeId = "di-default" };
            o.AutosaveDelay = TimeSpan.FromMilliseconds(25);
        });

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<ISettingsDocumentStore<TestDocument>>();

        store.Preview(new TestDocument { ThemeId = "di-preview" });
        Assert.Equal("di-preview", store.Effective.ThemeId);
    }

    [Fact]
    public void EffectiveChanged_fires_on_preview()
    {
        var path = CreateTempFilePath();
        using var store = CreateStore(path);
        var count = 0;
        store.EffectiveChanged += (_, _) => count++;

        store.Preview(new TestDocument { ThemeId = "a" });
        store.Preview(new TestDocument { ThemeId = "b" });

        Assert.Equal(2, count);
    }

    [Fact]
    public void Linux_path_resolver_uses_application_data_not_etc()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var path = SettingsPathResolver.ResolveJsonFilePath(new SettingsFileOptions
        {
            AppName = "ComposableSettingsTest",
            FileName = "settings.json",
        });

        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}etc{Path.DirectorySeparatorChar}", path);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.StartsWith(appData, path, StringComparison.Ordinal);
    }
}
