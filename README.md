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
<PackageReference Include="ComposableSettings" Version="1.0.*" />
```

Targets `net10.0`. The source generators ship inside the package as analyzers, so
they activate automatically on install — no extra reference needed.

Versioning follows CI run numbers (`1.0.{run}`) on pushes to `main` (see
`.github/workflows/publish-nuget.yml`). The `<Version>` in the csproj is a local placeholder;
**CI sets `PackageVersion` at pack time.**

**Actuator consumers** use `Version="1.0.*"` and pick up the latest publish on restore — do not
pin build numbers in actuator csproj. Cross-repo workflow:
[`actuator/docs/COMPOSABLESETTINGS_PACKAGE_WORKFLOW.md`](../actuator/docs/COMPOSABLESETTINGS_PACKAGE_WORKFLOW.md).

## Document profile (preview/commit)

For a single rich `settings.json` with factory defaults and a settings editor
that should **not** persist every slider tick:

```csharp
services.AddComposableSettingsDocument<AppSettings>(o =>
{
    o.FilePath = path;
    o.DefaultsFactory = () => new AppSettings();
    o.AutosaveDelay = TimeSpan.FromMilliseconds(750);
});
```

Use `store.Effective` for reads, `store.Preview(draft)` while editing,
`await store.CommitAsync(draft)` + `await store.FlushAsync()` on Save.

Layering (`AddComposableSettingsLayering`), packs (`AddComposableSettingsPacks`), export
(`AddComposableSettingsPackExporter`), and `SettingsEditingSession<T>` are available — see spec.

```csharp
services.AddComposableSettingsDocument<AppSettings>(o => { ... });
services.AddComposableSettingsLayering<AppSettings>(policy => { ... });
services.AddComposableSettingsPacks<AppSettings>(o => { ... }, doc => doc.ThemePack);
services.AddComposableSettingsPackExporter<AppSettings>();
```

See [docs/DOCUMENT-SETTINGS-PROFILE.md](docs/DOCUMENT-SETTINGS-PROFILE.md).

**Document editor VMs (preview/commit):** `[SettingsDraftVm]` — see
[docs/SETTINGS-DRAFT-VM.md](docs/SETTINGS-DRAFT-VM.md). Runtime: `SettingsDraftCommands`, `DraftMutation`.
Generator: `SettingsDraftVmGenerator` (nested `[SettingsProxy]` paths).

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

For slider-heavy screens, debounce only the persistence write while keeping the
live settings object synchronous:

```csharp
services.AddSettingsProvider<ClockSettings>(
    "gui",
    SettingsNodePath.Root("clock"),
    TimeSpan.FromMilliseconds(250));
```

### 3. Consume the live instance

Inject `ISettingsProvider<T>` and read/write `Current`:

```csharp
public  class Engine(ISettingsProvider<RuntimeSettings> settings)
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
`PropertyChanged` and persists each change. Use the debounced registration
overload to coalesce writes to the backing store without delaying `Current`.

### 4. (UI) ViewModels — `[SettingsVm]` vs `[SettingsConsumer]`

Pick the attribute by whether the class **already** implements `INotifyPropertyChanged`.

| Attribute | When to use | Generated init | INPC |
|-----------|-------------|----------------|------|
| **`[SettingsVm(typeof(T))]`** | MVVM with CommunityToolkit `ObservableObject` (or any existing INPC) | `InitializeSettings(provider)` | Relays into your `OnPropertyChanged` |
| **`[SettingsConsumer(typeof(T))]`** | Plain `partial` class with **no** INPC yet | `InitializeGeneratedSettings(provider)` | Generator **owns** INPC (CSP024 if you already have it) |

**Recommended for Avalonia/WPF MVVM** — `[SettingsVm]`:

```csharp
[SettingsVm(typeof(ClockSettings))]
public partial class ClockSettingsViewModel : ObservableObject, IDisposable
{
    public ClockSettingsViewModel(ISettingsProvider<ClockSettings> settings)
        => InitializeSettings(settings);

    [SettingsProxy] public partial bool IsGlslEnabled { get; set; }
    // manual projection when UI type ≠ model type:
    public string BaseColorHex
    {
        get => Settings.BaseColor;
        set => Settings.BaseColor = value;
    }

    public void Dispose() => DisposeGeneratedSettings();
}
```

Legacy / minimal hosts without an existing INPC base — `[SettingsConsumer]`:

```csharp
[SettingsConsumer(typeof(ClockSettings))]
public partial class ClockViewModel
{
    public ClockViewModel(ISettingsProvider<ClockSettings> settings)
        => InitializeGeneratedSettings(settings);
    // generated: `public ClockSettings Settings => provider.Current;` + INPC relay
}
```

Bind to `Settings.BaseColor`, `Settings.Glow.GlowIntensity`, or `[SettingsProxy]`
properties — edits flow straight through to disk, and external resets refresh the binding.

**One `[SettingsVm]` per class.** A dashboard that consumes multiple settings types
uses one primary `[SettingsVm]` and manual relays for the rest (see actuator case study).

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
(human-readable XML, one file per owner). `AddComposableSettingsJsonFile(key, path)` uses
`JsonSettingsFile` (System.Text.Json, full-rewrite per node).

**Format vs write strategy:** swapping XML/JSON/YAML is implementing
`IComponentSettingsProvider` (format/codec). A future **surgical / byte-indexed** write path
will be an **additive**, opt-in extension — the whole-object `Set` API stays the default.
See [`docs/PERSISTENCE_EXTENSIBILITY.md`](docs/PERSISTENCE_EXTENSIBILITY.md).

To plug a different backend, implement `IComponentSettingsProvider` and register it under a key:

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
| `SettingsConsumerGenerator` | `[SettingsConsumer(typeof(T))] partial class` (no INPC yet) | `Settings` pass-through + `InitializeGeneratedSettings(provider)` + generator-owned INPC |
| `ObservableSettingsGenerator` | `[SettingsVm(typeof(T))] partial class` (existing INPC) | `Settings` pass-through + `InitializeSettings(provider)` + relay into `OnPropertyChanged` + `[SettingsProxy]` bodies + `DisposeGeneratedSettings()` — **preferred for MVVM** |

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
| CSP030 | `[SettingsVm]` class must be `partial` |
| CSP031 | `[SettingsVm]` requires a settings type argument |
| CSP032 | `[SettingsProxy]` has no matching settings model member |
| CSP033 | `[SettingsProxy]` type does not match the settings member type |

## Conventions & limitations

- Observable lists are `ObservableCollection<T>`. Collection **item** types should
  be `[SettingsModel]` if you want in-place item edits to persist; value-like
  items (string/primitive/enum) persist on add/remove/replace.
- Nested-object fields must be non-`readonly`.
- `Dictionary<,>` is not supported.
- Settings models are POCOs with `[A-Za-z0-9_.-]+` node names; nested classes as
  the model itself are not supported.

## Examples & design notes

See [`examples/`](examples/):

- [`examples/migrating-from-a-central-registry.md`](examples/migrating-from-a-central-registry.md)
  — step-by-step migration from a hand-wired central settings registry (Actuator case study;
  includes `clock.json`, `[SettingsVm]`, and multi-slice VMs).
- [`docs/PERSISTENCE_EXTENSIBILITY.md`](docs/PERSISTENCE_EXTENSIBILITY.md)
  — format (codec) vs write strategy; future surgical writes without breaking `Set<T>`.
- [`docs/DOCUMENT-SETTINGS-PROFILE.md`](docs/DOCUMENT-SETTINGS-PROFILE.md)
  — **Document profile** (design): single rich `settings.json`, preview/commit,
  layered defaults, theme packs; same NuGet, opt-in `AddComposableSettingsPacks()` etc.
  Reference consumer: NanoCommander.
