# ComposableSettings

Component-based settings composition with source-generated child wiring, registration, and dependency injection.

## Problem

Traditional settings management requires a central registry that knows about every settings type. Adding one settings type requires touching many files across the abstraction, engine, and view-model layers.

**ComposableSettings** inverts this dependency: each component declares its own settings model. A source generator walks the component tree and emits wiring, registration, and DI code automatically.

## Installation

```xml
<PackageReference Include="ComposableSettings" Version="1.0.1" />
```

## Minimal Setup

```csharp
using ComposableSettings;

var services = new ServiceCollection();

// Core infrastructure (factory, context, component settings resolution)
services.AddComposableSettingsInfrastructure();

// Your store implementation
services.AddSingleton<IComponentSettingsStore, InMemoryComponentSettingsStore>();
// or: services.AddInMemoryComposableSettingsStore();

// Generated: registers all [SettingsRoot]/[SettingsComponent] types
services.AddGeneratedSettingsComponents();

// Generated: registers all discovered settings paths with the store
services.AddSingleton<IComponentSettingsInitializer, GeneratedComponentSettingsInitializer>();

var provider = services.BuildServiceProvider();
provider.GetRequiredService<IComponentSettingsInitializer>().Initialize(resetToDefaults: false);
```

## Key Concepts

### Component Tree

```csharp
[SettingsRoot("gui")]
public sealed partial class RootViewModel
{
    public RootViewModel(ISettingsNodeFactory factory)
    {
        InitializeGeneratedSettingsChildren(factory, SettingsNodePath.Root("gui"));
    }

    [SettingsChild("appearance")]
    public AppearanceViewModel Appearance { get; private set; } = null!;
}

[SettingsComponent("appearance")]
public sealed partial class AppearanceViewModel
{
    public AppearanceViewModel(ISettingsNodeContext context, ISettingsNodeFactory factory)
    {
        InitializeGeneratedSettingsChildren(factory, context.Path);
    }

    [SettingsChild("clock")]
    public ClockViewModel Clock { get; private set; } = null!;
}

// Hand-written component: it declares its own constructor, so it opts OUT
// of the generated lifecycle (see "Generated Settings Lifecycle" below).
[SettingsComponent("clock", typeof(ClockSettings), GenerateLifecycle = false)]
public sealed class ClockViewModel
{
    private readonly IComponentSettings<ClockSettings> _settings;

    public ClockViewModel(IComponentSettings<ClockSettings> settings)
    {
        _settings = settings;
    }

    public Task SaveAsync(ClockSettings value) => _settings.SaveAsync(value);
}
```

### Paths

Component paths are built as `gui/appearance/clock` using `SettingsNodePath.Root("gui").Child("appearance").Child("clock")`.

Valid path segment characters: `[A-Za-z0-9_.-]+`

### Source Generators (3)

| Generator | What it emits |
|-----------|---------------|
| `SettingsChildrenGenerator` | `InitializeGeneratedSettingsChildren(factory, path)` for `partial` parent classes |
| `SettingsRegistrationGenerator` | `GeneratedComponentSettingsInitializer` that calls `store.Register<T>(path)` for all leafs |
| `SettingsDIGenerator` | `AddGeneratedSettingsComponents(this IServiceCollection)` extension |

### Store

Implement `IComponentSettingsStore` to connect your persistence backend:

```csharp
public interface IComponentSettingsStore
{
    TSettings Get<TSettings>(SettingsNodePath path) where TSettings : class, new();
    void Set<TSettings>(SettingsNodePath path, TSettings value) where TSettings : class, new();
    void Register<TSettings>(SettingsNodePath path) where TSettings : class, new();
    void CompleteRegistration(bool resetToDefaults = false);
}
```

A simple `InMemoryComponentSettingsStore` is included for testing and non-persistent scenarios.

## Generated Settings Lifecycle

ComposableSettings is **lifecycle/async-first**: it generates a minimal async
settings lifecycle for every component that declares a settings type. This is
**opt-out**, not opt-in.

- A component with a settings type generates the lifecycle **by default** — no
  attribute argument needed.
- Set `GenerateLifecycle = false` to make a component a pure grouping/tree node
  or to keep a hand-written component (e.g. one with its own constructor).
- A `[SettingsComponent("name")]` **without** a settings type is always a pure
  grouping node — no lifecycle, no diagnostic.

```csharp
// Lifecycle generated automatically (settings type present, no opt-out):
[SettingsComponent("sleepMode", typeof(SleepModeSettings))]
public partial class SleepModeComponent
{
}

// Explicit opt-out:
[SettingsComponent("sleepMode", typeof(SleepModeSettings), GenerateLifecycle = false)]
public sealed class HandWrittenSleepModeComponent { /* your own code */ }
```

The source generator emits:

```csharp
public partial class SleepModeComponent
{
    private readonly IComponentSettings<SleepModeSettings> _componentSettings;

    public SleepModeComponent(
        IComponentSettings<SleepModeSettings> componentSettings)
    {
        _componentSettings = componentSettings;
        Settings = new SleepModeSettings();
    }

    public SleepModeSettings Settings { get; private set; } = new();

    public async Task ResetSettingsAsync(CancellationToken ct = default)
    {
        Settings = new SleepModeSettings();
        await SettingsUpdatedAsync(Settings, ct);
    }

    public async Task SaveSettingsAsync(CancellationToken ct = default)
    {
        await _componentSettings.SaveAsync(Settings, ct);
    }

    protected virtual Task SettingsUpdatedAsync(
        SleepModeSettings settings, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
```

Key behaviors:
- **Reset** creates a fresh settings instance, then notifies the component
- **Save** is explicit — it only calls `_componentSettings.SaveAsync()`, never auto-saves
- GUI code decides when to call `SaveSettingsAsync()` (e.g., after a save button, debounce, or screen close)
- Override `SettingsUpdatedAsync()` for UI refresh/recalculation on reset
- The component must be `partial` and the settings type must have a public parameterless constructor
- A constructor accepting `IComponentSettings<TSettings>` is generated
- **Lifecycle generation supports only components without user-defined constructors.** If the component declares its own constructor, the generator reports diagnostic CSP015 and skips generation. Constructor merging/injection augmentation is intentionally deferred.

## Limitations

- Nested parent classes are not supported
- Dynamic children and collections are not supported
- Path segments limited to `[A-Za-z0-9_.-]+`
- No change bus or preview mechanism yet
- `SaveAsync` may be synchronous depending on store implementation
- Lifecycle generator requires `partial` class and public parameterless settings constructor
