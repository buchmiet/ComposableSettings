# ComposableSettings

Observable, auto-persisting application settings with source-generated boilerplate.

Each settings model declares itself; a source generator gives it
`INotifyPropertyChanged`; a provider hands your code one **live** instance that
**auto-persists on every change** — including edits nested arbitrarily deep.
Different owners (e.g. *runtime* vs *gui*) keep their settings in **separate
files**, registered independently.

## Why

Traditional settings management funnels everything through a central registry
that must know about every settings type — adding one setting means touching the
model, a snapshot DTO, an `UpdateXAsync` method, a path constant, and DI wiring.

ComposableSettings inverts that: a model owns its shape, the generator owns the
plumbing, and a consumer just edits a live object.

```csharp
// No Save(), no Update method, no snapshot rebuild:
clock.Current.BaseColor = "#00FF00";   // persisted automatically; bindings update live
```

## Install

```xml
<PackageReference Include="ComposableSettings" Version="1.0.11" />
```

Targets `net10.0`. The source generators ship inside the package as analyzers, so
they activate automatically on install — no extra reference needed.

## Quick start

### 1. Declare settings models

Mark a `partial` class `[SettingsModel]` and write private `_camelCase` fields.
The generator emits `INotifyPropertyChanged` and a public property per field
(defaults come from the field initializers).

```csharp
using System.Collections.ObjectModel;
using ComposableSettings;

[SettingsModel]
public partial class ClockSettings
{
    private string _baseColor = "#e6194b";   // -> public string BaseColor { get; set; }
    private double _brightness = 0.8;          // -> public double Brightness { get; set; }
    private GlowSettings _glow = new();        // nested model (tracked deeply)
}

[SettingsModel]
public partial class GlowSettings
{
    private int _waveFrequency = 24;
    private double _glowIntensity = 0.18;
}

[SettingsModel]
public partial class RuntimeSettings
{
    private string _pluginsFolder = "./plugins";
    private ObservableCollection<string> _enabledPlugins = new();   // observable collection
}
```

Field kinds the generator understands:

| Field type | Generated member | Notes |
|---|---|---|
| scalar — `string`, value type, enum | change-raising property | default from initializer |
| `ObservableCollection<T>` | get-only property | add/remove **and** item edits tracked; `readonly` field is fine |
| other reference type (nested model) | property with a re-tracking setter | field must be **non-readonly** |

### 2. Register one file per owner + a provider per node

```csharp
using ComposableSettings;
using ComposableSettings.Runtime; // SettingsNodePath

services.AddComposableSettingsFile("gui", guiXmlPath);        // owner: gui
services.AddSettingsProvider<ClockSettings>("gui", SettingsNodePath.Root("clock"));

services.AddComposableSettingsFile("runtime", runtimeXmlPath); // owner: runtime
services.AddSettingsProvider<RuntimeSettings>("runtime", SettingsNodePath.Root("runtime"));
```

`gui.xml` and `runtime.xml` are independent: the runtime owner does not know about
GUI settings and vice versa.

### 3. Consume the live instance

Inject `ISettingsProvider<T>` and read/write `Current`:

```csharp
public sealed class Engine(ISettingsProvider<RuntimeSettings> settings)
{
    public void Run()
    {
        var folder = settings.Current.PluginsFolder;     // read
        settings.Current.EnabledPlugins.Add("Plugin.A"); // mutate -> auto-persists
    }
}
```

```csharp
public interface ISettingsProvider<TSettings>
    where TSettings : class, INotifyPropertyChanged, new()
{
    TSettings Current { get; }              // the live, observable instance
    event EventHandler<TSettings>? Replaced; // raised on Reset/Reload
    void Reset();                            // restore defaults (persisted)
    void Reload();                           // re-read from the backing file
}
```

There is **no `Save`**: the provider subscribes to the instance's
`PropertyChanged` and persists each change (debounce in your store if you want
batching).

### 4. (UI) Consumers that bind, with zero stored state

For ViewModels that bind a settings model, mark them `[SettingsConsumer(typeof(T))]`
and call the generated `InitializeGeneratedSettings` from your own constructor:

```csharp
[SettingsConsumer(typeof(ClockSettings))]
public partial class ClockViewModel
{
    public ClockViewModel(ISettingsProvider<ClockSettings> settings /*, other deps */)
        => InitializeGeneratedSettings(settings);
    // generated: `public ClockSettings Settings => provider.Current;` + INPC relay
}
```

Bind to `Settings.BaseColor`, `Settings.Glow.GlowIntensity`, … — edits flow
straight through to disk, and external resets refresh the binding.

## Deep observability

A change at any depth propagates up to the owning model's `PropertyChanged`, so
the provider persists it:

```csharp
gui.Current.Clock.Glow.GlowIntensity = 0.5;   // grandchild edit -> persisted
palette.Current.Colors.Add("#123456");         // collection add -> persisted
schedules.Current.Jobs[0].Cron = "*/5 * * * *"; // item edit -> persisted (items are [SettingsModel])
```

This is wired by the generated constructor via `SettingsChangeTracking`
(collections re-sync item subscriptions across add/remove/replace/clear).

## Persistence / custom stores

`AddComposableSettingsFile(key, path)` uses the built-in `XmlSettingsFile`
(human-readable XML, one file per owner). To plug a different backend, implement
`IComponentSettingsProvider` and register it under a key:

```csharp
public interface IComponentSettingsProvider
{
    TSettings Get<TSettings>(SettingsNodePath path) where TSettings : class, new();
    void Set<TSettings>(SettingsNodePath path, TSettings value) where TSettings : class, new();
}

services.AddComposableSettingsFile("gui", new MyJsonStore(path));
```

Node paths address a model within a file: `SettingsNodePath.Root("gui").Child("clock")`
(`gui/clock`). Valid segment characters: `[A-Za-z0-9_.-]+`.

## Source generators

| Generator | Trigger | Emits |
|---|---|---|
| `SettingsModelGenerator` | `[SettingsModel] partial class` | `INotifyPropertyChanged`, properties (scalar / collection / nested), and a constructor that tracks nested members |
| `SettingsConsumerGenerator` | `[SettingsConsumer(typeof(T))] partial class` | `Settings` pass-through property + `InitializeGeneratedSettings(provider)` + INPC relay |

### Diagnostics

| Id | Meaning |
|---|---|
| CSP020 | `[SettingsModel]` class must be `partial` |
| CSP021 | `[SettingsModel]` class must not already implement `INotifyPropertyChanged` |
| CSP022 | `[SettingsConsumer]` class must be `partial` |
| CSP023 | `[SettingsConsumer]` requires a settings type argument |
| CSP024 | `[SettingsConsumer]` class must not already implement `INotifyPropertyChanged` |
| CSP025 | `[SettingsModel]` with tracked members must not declare a constructor (the generator owns it) |
| CSP026 | a nested-object field must be non-`readonly` (the generator owns its setter; `readonly` is fine for collections) |

## Conventions & limitations

- Observable lists are `ObservableCollection<T>`. Collection **item** types should
  be `[SettingsModel]` if you want in-place item edits to persist; value-like
  items (string/primitive/enum) persist on add/remove/replace.
- Nested-object fields must be non-`readonly`.
- `Dictionary<,>` is not supported.
- Settings models are POCOs with `[A-Za-z0-9_.-]+` node names; nested classes as
  the model itself are not supported.

## Examples

See [`examples/`](examples/):

- [`examples/migrating-from-a-central-registry.md`](examples/migrating-from-a-central-registry.md)
  — a step-by-step migration from a hand-wired central settings registry to
  ComposableSettings (real-world case study).
