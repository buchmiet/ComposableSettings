# Document Settings Profile — design spec

> **Status:** design / API proposal (not yet implemented)  
> **Date:** 2026-06-29  
> **Motivation:** apps like **NanoCommander** need a richer settings model than live
> auto-persist slices — one user document, layered defaults, theme packs, and a
> settings editor with **preview/commit**. This profile adds that on top of the
> existing **Composable** profile without splitting into multiple NuGet packages.

Related docs:

- [README](../README.md) — Composable profile (live `ISettingsProvider<T>`)
- [PERSISTENCE_EXTENSIBILITY.md](./PERSISTENCE_EXTENSIBILITY.md) — codec / write strategy
- [migrating-from-a-central-registry.md](../examples/migrating-from-a-central-registry.md) — Actuator case study

Reference consumer (external): **NanoCommander.Avalonia** — `NanoCommander.Settings`
(`SettingsStore`, `ThemePackService`, `SettingsEditingSession`, `ThemePackSettingsMerger`).

---

## 1. Two profiles, one package

| | **Composable profile** (today) | **Document profile** (proposed) |
|--|-------------------------------|----------------------------------|
| **Mental model** | Many small settings slices | One (or few) rich documents |
| **Edit style** | Live — every change persists | Draft → preview → commit |
| **Persistence** | Per-slice file/node | User layer file; effective = merged layers |
| **Layers** | None | defaults → optional pack → user |
| **DI entry** | `AddSettingsProvider<T>` | `AddComposableSettingsDocument<T>` + opt-in modules |
| **Typical app** | Actuator, engines, plugins | NanoCommander, theme-heavy desktop/TUI |

Both profiles ship in the **same** `ComposableSettings` NuGet. Document modules are
**opt-in via DI registration** — if you never call `AddComposableSettingsPacks()`,
no pack loader, cache directory, or zip handling is registered (code may live in
the same assembly; behavior is off).

**Why one package:** shared versioning, simpler consumption across your repos, and
only tens of KB extra IL — acceptable vs maintaining 3–4 coordinated packages.

---

## 2. Module layout (internal)

Proposed source layout inside `src/ComposableSettings/`:

```
ComposableSettings/
├── Observable/          # existing — Composable profile
├── Stores/              # existing — JsonSettingsFile, XmlSettingsFile
├── Runtime/             # existing — IComponentSettingsProvider, SettingsNodePath
├── Document/            # NEW — document store, editing session, effective resolution
│   ├── ISettingsDocumentStore.cs
│   ├── SettingsDocumentStore.cs
│   ├── SettingsDocumentOptions.cs
│   └── DocumentServiceCollectionExtensions.cs
├── Layering/            # NEW — merge policies, JSON deep merge
│   ├── ISettingsLayerMerger.cs
│   ├── SettingsMergePolicy.cs
│   ├── JsonDeepMerge.cs
│   └── LayeringServiceCollectionExtensions.cs
└── Packs/               # NEW — zip/dir packs, cache, export
    ├── ISettingsPackLoader.cs
    ├── ISettingsPackCatalog.cs
    ├── ISettingsPackExporter.cs
    ├── SettingsPackService.cs
    └── PacksServiceCollectionExtensions.cs
```

Namespaces mirror folders (`ComposableSettings.Document`, `.Layering`, `.Packs`).
Public API surface grows only when the matching `Add*` extension is used.

---

## 3. Core abstraction: `ISettingsDocumentStore<TDocument>`

Replaces the ad-hoc combination of monolithic store + preview/commit in consumer apps.

```csharp
namespace ComposableSettings.Document;

/// <summary>
/// Holds the effective settings (after layer merge) and supports
/// non-persisting preview edits plus explicit commit of the user layer.
/// </summary>
public interface ISettingsDocumentStore<TDocument>
    where TDocument : class, new()
{
    /// <summary>Effective settings after defaults / pack / user merge.</summary>
    TDocument Effective { get; }

    /// <summary>User-owned slice only (what is written to disk).</summary>
    TDocument UserLayer { get; }

    /// <summary>Raised when <see cref="Effective"/> changes (preview or commit).</summary>
    event EventHandler? EffectiveChanged;

    /// <summary>Apply in-memory preview; does not write user file.</summary>
    void Preview(TDocument userLayerDraft);

    /// <summary>Persist user layer (debounced or immediate per options).</summary>
    Task CommitAsync(TDocument userLayerDraft, CancellationToken cancellationToken = default);

    /// <summary>Re-read user file and rebuild effective.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>Reset user layer to factory defaults and persist.</summary>
    Task ResetUserLayerAsync(CancellationToken cancellationToken = default);
}
```

### Semantics (aligned with NanoCommander)

| Operation | NanoCommander today | Document profile |
|-----------|---------------------|------------------|
| Startup load | `SettingsStore` ctor → merge pack | `Effective` built once |
| Settings slider tick | `ApplyPreview` | `Preview(draft)` |
| Save button | `CommitEditing` | `CommitAsync(draft)` |
| Cancel | `SettingsEditingSession.ResetFromBaseline` | session in consumer VM; store reload optional |
| App chrome reads settings | `SettingsStore.Current` | `documentStore.Effective` |

**Important:** `Preview` updates `Effective` immediately for UI binding but **does not**
schedule disk write. This is the opposite of `ISettingsProvider<T>.Current`, where
every `PropertyChanged` persists.

---

## 4. DI registration — opt-in modules

### 4.1 Minimal document (no packs, no layering)

For apps with a single JSON file and preview/commit only:

```csharp
services.AddComposableSettingsDocument<AppSettings>(options =>
{
    options.FilePath = userSettingsPath;
    options.DefaultsFactory = () => AppSettings.CreateFactoryDefaults();
    options.AutosaveDelay = TimeSpan.FromMilliseconds(750);
    options.UseAtomicWrites = true;
});
```

Registers: `ISettingsDocumentStore<AppSettings>` (singleton).

### 4.2 Document + layering

When effective settings = defaults ⊕ pack overlay ⊕ user overlay:

```csharp
services.AddComposableSettingsDocument<AppSettings>(...);

services.AddComposableSettingsLayering<AppSettings>(policy =>
{
  policy.MergeableRootProperties.Add("layout");
  policy.MergeableRootProperties.Add("themeID");
  policy.UserMergeMode = SettingsMergeMode.DeepMergeNonDefault;
  policy.ExcludedFromPackMerge.Add("window");
  policy.ExcludedFromPackMerge.Add("shell");
});
```

Registers: `ISettingsLayerMerger<AppSettings>` used inside `SettingsDocumentStore`.

Maps to NanoCommander `ThemePackSettingsMerger.MergeCascade` + field exclusions.

### 4.3 Document + layering + packs

```csharp
services.AddComposableSettingsPacks(options =>
{
    options.PacksDirectory = Path.Combine(appSupport, "packs");
    options.CacheDirectory = Path.Combine(appSupport, "cache", "packs");
    options.Extension = ".nctheme"; // consumer-chosen; generic loader
});

services.AddComposableSettingsPackExporter<AppSettings>();
```

Registers: `ISettingsPackCatalog`, `ISettingsPackLoader`, `ISettingsPackExporter`.
**Not called → none of the above exist in DI.**

### 4.4 Full NanoCommander-like stack

```csharp
services.AddComposableSettingsDocument<AppSettings>(o => { ... });
services.AddComposableSettingsLayering<AppSettings>(NcMergePolicy.Configure);
services.AddComposableSettingsPacks(o => { ... });
services.AddComposableSettingsPackExporter<AppSettings>();
```

---

## 5. Layering module

### 5.1 Merge pipeline

```
Effective = LayerMerger.Merge(
    defaults:  options.DefaultsFactory(),
    pack:      packCatalog.ActivePack?.SettingsOverlay,   // optional
    user:      loaded from user JSON file
);
```

Only the **user** layer is serialized to `settings.json`. Pack and defaults are
reconstructed on each load.

### 5.2 `ISettingsLayerMerger<TDocument>`

```csharp
public interface ISettingsLayerMerger<TDocument> where TDocument : class, new()
{
    TDocument Merge(TDocument defaults, TDocument? packOverlay, TDocument userLayer);
}
```

Default implementation: **JSON deep merge** (proven in NanoCommander
`ThemePackSettingsMerger`) with declarative policy:

```csharp
public sealed class SettingsMergePolicy
{
    public HashSet<string> MergeableRootProperties { get; } = new();
    public HashSet<string> ExcludedFromPackMerge { get; } = new();
    public SettingsMergeMode UserMergeMode { get; set; } = SettingsMergeMode.DeepMergeNonDefault;
}

public enum SettingsMergeMode
{
    Replace,
    DeepMerge,
    DeepMergeNonDefault,  // skip overlay values equal to factory defaults
}
```

Typed merge (without JSON round-trip) is a **future optimization**; v1 can reuse
the JSON approach for correctness and tolerance of partial documents.

### 5.3 Extract from NanoCommander

| NanoCommander | Document profile |
|---------------|------------------|
| `ThemePackSettingsMerger.MergeCascade` | `SettingsLayerMerger<T>.Merge` |
| `MergeThemeLayout` | policy-driven property subset |
| `DeepMergeNonDefault` | `SettingsMergeMode.DeepMergeNonDefault` |
| `ThemePackService.ApplyPackLayer` | pack module hook on load/preview/commit |

---

## 6. Packs module

Generic **settings pack** — not NC-specific naming in API.

### 6.1 Pack layout (on disk / zip)

```
my-theme.settingspack   (or .nctheme, .zip — extension is convention)
├── pack.json           manifest (id, name, version)
├── settings.overlay.json
└── assets/             optional: fonts, icons, shaders (opaque to core)
    ├── fonts/
    └── shaders/
```

### 6.2 Interfaces

```csharp
public interface ISettingsPackLoader
{
    Task<SettingsPackLoadResult> LoadAsync(string packPath, CancellationToken ct = default);
}

public interface ISettingsPackCatalog
{
    string? ActivePackId { get; }
    IReadOnlyList<SettingsPackInfo> ListInstalled();
    Task ActivateAsync(string packId, CancellationToken ct = default);
}

public interface ISettingsPackExporter<TDocument>
{
    Task ExportAsync(string outputPath, TDocument userLayer, CancellationToken ct = default);
}
```

### 6.3 Cache

Mirror NanoCommander `ThemePackArchive`:

- extract zip → `{CacheDirectory}/{packId}-{stamp}/`
- invalidate when archive timestamp changes

Pack **assets** (fonts/icons) are exposed via `SettingsPackContext` — consumers
(NC Views) resolve paths; ComposableSettings only guarantees extract + manifest.

### 6.4 Palette / product-specific parsing

**Out of scope** for the pack module. NanoCommander keeps
`ThemePackPaletteParser`, `CommanderThemeDefaults`, etc. locally. The pack module
delivers `settings.overlay.json` as `JsonElement` or deserialized `TDocument` slice.

---

## 7. Editing module & ViewModels

### 7.1 `SettingsEditingSession<T>` (optional helper in package)

Equivalent to NanoCommander `SettingsEditingSession`:

```csharp
public sealed class SettingsEditingSession<TDocument> where TDocument : class, new()
{
    public TDocument Draft { get; private set; }
    public void ResetFromBaseline(TDocument baseline);
    public int ChangeRevision { get; }
}
```

Lives in `ComposableSettings.Document` — consumer ViewModels can use it or keep
their own; NC can delete its copy after migration.

### 7.2 Generator integration (future)

| Attribute | Profile | Behavior |
|-----------|---------|----------|
| `[SettingsVm]` | Composable | Binds to `ISettingsProvider<T>.Current` (live) |
| `[SettingsDraftVm]` | Document | Binds to `SettingsEditingSession<T>` draft; Save calls `CommitAsync` |

Not required for v1 — manual wiring like NC today is fine.

---

## 8. Persistence hardening (shared by both profiles)

Improvements that benefit Document profile and optionally Composable stores:

| Feature | NanoCommander | ComposableSettings today | Document v1 |
|---------|---------------|--------------------------|-------------|
| Atomic write (`.tmp` + move) | yes | no (direct `WriteAllText`) | **yes** |
| Debounced save | Debouncer.Sharp 750ms | Debouncer.Sharp | same |
| Tolerant deserialize | yes (per-field) | catch → `new()` | **Normalize hook** |
| Sorted JSON keys | yes | no | optional `WriteOptions` |
| Linux path | `%AppData%` / XDG | `/etc/{app}` | **fix `SettingsPathResolver`** to XDG `~/.config` |
| Schema version | no | no | optional `documentVersion` field + migrators |

```csharp
public interface ISettingsDocumentSerializer<TDocument>
{
    TDocument Deserialize(string json, TDocument defaults);
    string Serialize(TDocument value);
    TDocument Clone(TDocument value);  // round-trip or memberwise
}
```

Default: `System.Text.Json` + consumer-supplied `Action<TDocument>? Normalize`.

---

## 9. What stays in consumer apps (NanoCommander)

Even after full Document profile adoption:

| Stay in NC | Why |
|------------|-----|
| `AppSettings`, `LayoutSettings`, enums | Product / OFM domain |
| `VisualPresetCatalog`, theme palettes | Branding |
| `ShellSettingsCatalog` | Platform probes → belongs in `Platform.*` |
| `CommanderThemeDefaults`, shader catalogs | Rendering coupling |
| `IOverlayShaderCatalog` impl | Views layer |
| Section `*SettingsViewModel` | UI-specific |

| Move to ComposableSettings | Why |
|----------------------------|-----|
| `SettingsStore` persist/preview/commit | Generic document store |
| `ThemePackSettingsMerger` | `Layering` module |
| `ThemePackArchive` / cache | `Packs` module |
| `SettingsEditingSession` | `Document` module |
| Atomic save, XDG paths | `Stores` / `SettingsPathResolver` |

Estimated extraction: **~30–40%** of `NanoCommander.Settings` LOC.

---

## 10. Rules — do not mix profiles blindly

### 10.1 Same `TDocument` — pick one write path

| OK | Not OK |
|----|--------|
| `ISettingsDocumentStore<AppSettings>` for NC shell | `AddSettingsProvider<AppSettings>` **and** `AddComposableSettingsDocument<AppSettings>` on the same file without explicit split |
| `ISettingsProvider<PluginSettings>` for plugins | Editing `LayoutSettings` via live provider while Document store also owns layout |

### 10.2 Analyzer / doc warnings (future)

- **CSP040:** `T` registered in both Composable and Document profiles
- **CSP041:** `Preview` called from generated `[SettingsVm]` bound to Document store

### 10.3 Consolonia / TUI

Document profile is **UI-agnostic**. NanoCommander TUI reads `Effective` from the
same `ISettingsDocumentStore<AppSettings>` — shared `settings.json` with GUI.

---

## 11. Implementation phases

| Phase | Deliverable | NC unblocks |
|-------|-------------|-------------|
| **D1** | `ISettingsDocumentStore`, atomic `JsonDocumentFile`, `AddComposableSettingsDocument`, XDG path fix | preview/commit without packs |
| **D2** | `Layering` — policy + JSON merge (port `ThemePackSettingsMerger`) | themepack overlay merge |
| **D3** | `Packs` — zip loader, cache, catalog, `AddComposableSettingsPacks` | `.nctheme` support |
| **D4** | `ISettingsPackExporter`, `SettingsEditingSession<T>` helper | export + VM cleanup |
| **D5** | `[SettingsDraftVm]` generator (optional) | less boilerplate in settings VMs |
| **D6** | NC migration spike — one module behind feature flag | validate in production schema |

Each phase: tests in `ComposableSettings.Tests`, optional adapter test in NC.

---

## 12. Example — NanoCommander registration (target state)

```csharp
// App.axaml.cs — Document profile (target)
services.AddComposableSettingsDocument<AppSettings>(o =>
{
    o.FilePath = AppPaths.SettingsFilePath;
    o.DefaultsFactory = VisualPresetCatalog.CreateFactoryDefaults;
    o.AutosaveDelay = TimeSpan.FromMilliseconds(750);
});

services.AddComposableSettingsLayering<AppSettings>(NcMergePolicies.ThemeLayout);

services.AddComposableSettingsPacks(o =>
{
    o.PacksDirectory = AppPaths.ThemepacksDirectory;
    o.CacheDirectory = AppPaths.ThemepackCacheDirectory;
});

services.AddComposableSettingsPackExporter<AppSettings>();

// Singleton for ViewModels / Views
services.AddSingleton<ISettingsDocumentStore<AppSettings>>(sp => sp.GetRequiredService<...>());
```

`MainWindowViewModel` injects `ISettingsDocumentStore<AppSettings>`,
uses `.Effective` instead of `SettingsStore.Current`.

Settings window:

```csharp
var session = new SettingsEditingSession<AppSettings>(store.Effective);
// on change: store.Preview(session.Draft);
// on save:   await store.CommitAsync(session.Draft);
```

---

## 13. Comparison with Actuator (Composable profile)

| Actuator | NanoCommander |
|----------|---------------|
| `clock.json` + `gui.xml` slices | single `settings.json` user layer |
| live edit per slice | preview/commit for whole editor |
| no packs | `.nctheme` packs |
| `AddSettingsProvider<ClockSettings>` | `AddComposableSettingsDocument<AppSettings>` |

Same package, different `Add*` entry points — **by design**.

---

## 14. Open questions

1. **INPC on document models** — required for `[SettingsDraftVm]`, or plain POCO + manual `EffectiveChanged`?
2. **Pack asset protocol** — minimal manifest schema in core vs consumer-extended `pack.json`?
3. **Migration API** — built-in `documentVersion` migrators or consumer-owned?
4. **Html dump / diagnostics** — out of scope for ComposableSettings?

Record decisions in this file as phases land.

---

*Spec v1.0 — single NuGet, opt-in Document / Layering / Packs modules via DI.*
