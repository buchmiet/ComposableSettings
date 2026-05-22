using ComposableSettings.Abstractions;
using ComposableSettings.Attributes;
using ComposableSettings.DependencyInjection;
using ComposableSettings.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings.Tests;

public  class SettingsNodeTests
{
    [Fact]
    public void Reusable_component_under_different_parents_gets_different_paths()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISettingsNodeFactory>();

        var child1 = factory.CreateChild<LeafViewModel>(SettingsNodePath.Root("gui").Child("component-y"), "cX");
        var child2 = factory.CreateChild<LeafViewModel>(SettingsNodePath.Root("gui").Child("component-z"), "cX");

        Assert.NotEqual(child1.Path.ToString(), child2.Path.ToString());
        Assert.Equal("gui/component-y/cX", child1.Path.ToString());
        Assert.Equal("gui/component-z/cX", child2.Path.ToString());
    }

    [Fact]
    public void Reusable_component_stores_independent_settings()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISettingsNodeFactory>();

        var child1 = factory.CreateChild<LeafViewModel>(SettingsNodePath.Root("gui").Child("p1"), "x");
        var child2 = factory.CreateChild<LeafViewModel>(SettingsNodePath.Root("gui").Child("p2"), "x");

        child1.SaveText("from-p1");
        child2.SaveText("from-p2");

        Assert.Equal("from-p1", child1.ReadText());
        Assert.Equal("from-p2", child2.ReadText());
    }

    [Fact]
    public void Context_does_not_leak_after_factory_create_child()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISettingsNodeFactory>();
        _ = factory.CreateChild<LeafViewModel>(SettingsNodePath.Root("gui"));

        var exception =
            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ISettingsNodeContext>());

        Assert.Contains("No settings node context is active", exception.Message);
    }

    [Fact]
    public void Direct_resolve_of_contextual_component_fails_without_context()
    {
        using var provider = CreateProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ParentViewModel>());

        Assert.Contains("No settings node context is active", exception.Message);
    }

    [Fact]
    public void Component_name_resolver_uses_type_name_without_viewmodel_suffix()
    {
        using var provider = CreateProvider();
        var resolver = provider.GetRequiredService<ISettingsComponentNameResolver>();

        Assert.Equal("PlainChild", resolver.GetComponentName(typeof(PlainChildViewModel)));
        Assert.Equal("PlainChild", resolver.GetComponentName(typeof(PlainChild)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/segment")]
    public void Invalid_path_segments_throw_clear_exception(string segment)
    {
        var exception = Assert.Throws<ArgumentException>(() => SettingsNodePath.Root(segment));
        Assert.Contains("Settings path segment", exception.Message);
    }

    [Theory]
    [InlineData("component-y")]
    [InlineData("cX")]
    [InlineData("primary_x")]
    [InlineData("chart.2d")]
    [InlineData("A1-b_2.c3")]
    public void Valid_path_segments_are_allowed(string segment)
    {
        var path = SettingsNodePath.Root(segment);
        Assert.Equal(segment, path.ToString());
    }

    [Fact]
    public void InMemory_store_distinguishes_same_path_with_different_settings_types()
    {
        using var provider = CreateProvider();
        var store = provider.GetRequiredService<IComponentSettingsStore>();
        var path = SettingsNodePath.Root("gui").Child("same-node");

        store.Set(path, new TestSettings { Text = "a", Counter = 7 });
        store.Set(path, new OtherSettings { Text = "b" });

        Assert.Equal("a", store.Get<TestSettings>(path).Text);
        Assert.Equal(7, store.Get<TestSettings>(path).Counter);
        Assert.Equal("b", store.Get<OtherSettings>(path).Text);
    }

    [Fact]
    public void Factory_creates_child_with_correct_path()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISettingsNodeFactory>();

        var child = factory.CreateChild<LeafViewModel>(SettingsNodePath.Root("gui").Child("parent"), "cX");

        Assert.Equal("gui/parent/cX", child.Path.ToString());
    }

    private static ServiceProvider CreateProvider()
    {
        return new ServiceCollection()
            .AddComposableSettingsProviders()
            .AddInMemoryComposableSettingsStore()
            .AddTransient<LeafViewModel>()
            .AddTransient<ParentViewModel>()
            .BuildServiceProvider(true);
    }

    [SettingsComponent("cX")]
    public  class LeafViewModel
    {
        private readonly IComponentSettings<TestSettings> _settings;

        public LeafViewModel(ISettingsNodeContext context, IComponentSettings<TestSettings> settings)
        {
            _settings = settings;
            Path = context.Path;
        }

        public SettingsNodePath Path { get; }

        public void SaveText(string text)
        {
            var value = _settings.Value;
            value.Text = text;
            value.Counter++;
            _settings.Save(value);
        }

        public string? ReadText()
        {
            return _settings.Value.Text;
        }
    }

    [SettingsComponent("parent")]
    public  class ParentViewModel
    {
        public ParentViewModel(ISettingsNodeContext context)
        {
            Path = context.Path;
        }

        public SettingsNodePath Path { get; }
    }

    public  class TestSettings
    {
        public string? Text { get; set; }
        public int Counter { get; set; }
    }

    private  class OtherSettings
    {
        public string? Text { get; set; }
    }

    private  class PlainChildViewModel;

    private  class PlainChild;
}