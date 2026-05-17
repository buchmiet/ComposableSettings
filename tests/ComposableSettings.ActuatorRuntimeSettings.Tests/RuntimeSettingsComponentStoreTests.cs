using Microsoft.Extensions.DependencyInjection;
using RuntimeSettings;

namespace ComposableSettings.ActuatorRuntimeSettings.Tests;

public sealed class RuntimeSettingsComponentStoreTests
{
    // Iteration 2 smoke test: proves the exact registration combo used by the
    // Actuator composition root (AddActuatorServer) resolves the new
    // ComposableSettings infrastructure and the RuntimeSettings store adapter.
    [Fact]
    public void Composition_root_registrations_resolve_from_di()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            using var provider = CreateProvider(tempRoot);

            Assert.NotNull(provider.GetRequiredService<ISettingsNodeFactory>());
            Assert.NotNull(provider.GetRequiredService<IComponentSettingsStore>());
        }
        finally { CleanupTempRoot(tempRoot); }
    }

    [Fact]
    public void Store_writes_to_xml_file()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            using var provider = CreateProvider(tempRoot);

            var store = provider.GetRequiredService<IComponentSettingsStore>();
            var path = SettingsNodePath.Root("gui").Child("appearance").Child("clock");

            store.Register<TestClockSettings>(path);
            store.CompleteRegistration(false);

            store.Set(path, new TestClockSettings { BaseColor = "#ABCDEF", Enabled = false });

            var settingsFile = Path.Combine(tempRoot, "PocTests", "settings.xml");
            Assert.True(File.Exists(settingsFile));

            var xml = File.ReadAllText(settingsFile);
            Assert.Contains("gui", xml);
            Assert.Contains("appearance", xml);
            Assert.Contains("clock", xml);
            Assert.Contains("#ABCDEF", xml);
            Assert.Contains("false", xml);
        }
        finally { CleanupTempRoot(tempRoot); }
    }

    [Fact]
    public void Generated_registration_writes_defaults_to_xml()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            using var provider = CreateProvider(tempRoot);

            var store = provider.GetRequiredService<IComponentSettingsStore>();
            var path = SettingsNodePath.Root("gui").Child("appearance").Child("clock");

            store.Register<TestClockSettings>(path);
            store.CompleteRegistration(false);

            var settingsFile = Path.Combine(tempRoot, "PocTests", "settings.xml");
            Assert.True(File.Exists(settingsFile));

            var xml = File.ReadAllText(settingsFile);
            Assert.Contains("#00FF00", xml);
        }
        finally { CleanupTempRoot(tempRoot); }
    }

    [Fact]
    public void Reset_to_defaults_overwrites_existing_values()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            using (var p = CreateProvider(tempRoot))
            {
                var store = p.GetRequiredService<IComponentSettingsStore>();
                var path = SettingsNodePath.Root("gui").Child("appearance").Child("clock");
                store.Register<TestClockSettings>(path);
                store.CompleteRegistration(false);

                store.Set(path, new TestClockSettings { BaseColor = "#FF0000", Enabled = false });

                var xml = File.ReadAllText(Path.Combine(tempRoot, "PocTests", "settings.xml"));
                Assert.Contains("#FF0000", xml);
            }

            using (var p = CreateProvider(tempRoot))
            {
                var store = p.GetRequiredService<IComponentSettingsStore>();
                var path = SettingsNodePath.Root("gui").Child("appearance").Child("clock");
                store.Register<TestClockSettings>(path);
                store.CompleteRegistration(true);

                var clock = store.Get<TestClockSettings>(path);
                Assert.Equal("#00FF00", clock.BaseColor);
                Assert.True(clock.Enabled);

                var xml = File.ReadAllText(Path.Combine(tempRoot, "PocTests", "settings.xml"));
                Assert.Contains("#00FF00", xml);
                Assert.Contains("<Enabled>true</Enabled>", xml);
            }
        }
        finally { CleanupTempRoot(tempRoot); }
    }

    [Fact]
    public void Round_trip_through_xml()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            using var provider = CreateProvider(tempRoot);
            var store = provider.GetRequiredService<IComponentSettingsStore>();
            var path = SettingsNodePath.Root("test").Child("node");

            store.Register<TestClockSettings>(path);
            store.CompleteRegistration(false);

            store.Set(path, new TestClockSettings { BaseColor = "#DEADBE", Enabled = false });
            var loaded = store.Get<TestClockSettings>(path);

            Assert.Equal("#DEADBE", loaded.BaseColor);
            Assert.False(loaded.Enabled);
        }
        finally { CleanupTempRoot(tempRoot); }
    }

    [Fact]
    public void Path_maps_correctly_from_SettingsNodePath_to_SettingsPath()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            using var provider = CreateProvider(tempRoot);
            var store = (RuntimeSettingsComponentStore)provider.GetRequiredService<IComponentSettingsStore>();
            var path = SettingsNodePath.Root("gui").Child("appearance").Child("clock");

            store.Register<TestClockSettings>(path);
            store.CompleteRegistration(false);

            var clock = store.Get<TestClockSettings>(path);
            Assert.NotNull(clock);
        }
        finally { CleanupTempRoot(tempRoot); }
    }

    private static ServiceProvider CreateProvider(string tempRoot)
    {
        var options = new RuntimeSettingsOptions
        {
            AppName = "PocTests",
            FileName = "settings.xml",
            Scope = SettingsScope.User,
            BaseDirectoryOverride = tempRoot
        };

        return new ServiceCollection()
            .AddComposableSettingsInfrastructure()
            .AddActuatorRuntimeSettingsStore(options)
            .BuildServiceProvider(validateScopes: true);
    }

    private static string CreateTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "ComposableSettingsAdapterTests", Guid.NewGuid().ToString("N"));
    }

    private static void CleanupTempRoot(string tempRoot)
    {
        try { Directory.Delete(tempRoot, recursive: true); } catch { }
    }

    public sealed class TestClockSettings
    {
        public string BaseColor { get; set; } = "#00FF00";
        public bool Enabled { get; set; } = true;
    }
}
