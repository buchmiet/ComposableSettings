# D5 — `[SettingsDraftVm]` generator spec

> **Status:** spec v1.0 (design) · generator not implemented yet  
> **Depends on:** Document profile D1–D4 (`ISettingsDocumentStore`, `SettingsEditingSession`)  
> **Motivation:** apps like **NanoCommander** and other rich MVVM desktops repeat the same
> draft → preview → commit glue in every settings section ViewModel. D5 moves that glue
> into ComposableSettings — the same way `[SettingsVm]` did for live-edit Composable profile.

Related:

- [DOCUMENT-SETTINGS-PROFILE.md](./DOCUMENT-SETTINGS-PROFILE.md) — Document profile overview
- [README](../README.md) — `[SettingsVm]` (Composable / live edit)

---

## 1. Problem statement

### Composable profile (solved — `[SettingsVm]`)

```csharp
clockVm.BaseColor = "#00FF00";  // writes ISettingsProvider.Current → auto-persist
```

Generator relays INPC from the live model into the VM. **No draft, no Save button.**

### Document profile (manual today — D5 target)

```csharp
session.Draft.Layout.Terminal.Padding.Horizontal = 12;
store.Preview(session.Draft);   // every slider tick — repeated in every setter
// Save: store.CommitAsync(session.Draft); session.UpdateBaseline();
```

**NanoCommander today:** ~15 section VMs × ~10–40 properties each → hundreds of lines of
`DraftPropertyGuard` + `ApplyPreview` + `OnPropertyChanged`. Same pattern will appear in
every app with a settings editor and preview/commit semantics.

**D5 goal:** declare `[SettingsDraftVm]` + `[SettingsProxy]` and let the generator emit
draft accessors, change guards, preview calls, and INPC relay — **DRY at the MVVM boundary.**

---

## 2. Non-goals

| Out of scope for D5 | Where it stays |
|---------------------|----------------|
| `AppSettings` / layout domain models | Consumer app |
| Theme pack assets (fonts, shaders, icons) | Consumer app |
| Section-specific reset logic (`ResetSectionDefaults`) | User `partial` method |
| Custom normalization in setters (e.g. font name) | Manual property or `partial void OnDraftMemberChanging` |
| `CommunityToolkit` `[RelayCommand]` on Save/Cancel | User code or thin app base class |
| Migrating `SettingsStore` → `ISettingsDocumentStore` | Epic D6 (can run in parallel with D5 pilot) |

---

## 3. Two profiles, symmetric generators

| | **Composable** | **Document** |
|--|----------------|--------------|
| **Attribute** | `[SettingsVm(typeof(T))]` | `[SettingsDraftVm(typeof(T))]` |
| **Store** | `ISettingsProvider<T>` | `ISettingsDocumentStore<T>` |
| **Working copy** | `provider.Current` (live) | `session.Draft` |
| **On property set** | assign to live model → auto-persist | assign to draft → `store.Preview(draft)` |
| **Commit** | implicit | `store.CommitAsync(session.Draft)` + `session.UpdateBaseline()` |
| **Generator** | `ObservableSettingsGenerator` (exists) | `SettingsDraftVmGenerator` (D5) |

**Rule:** never mix both attributes on the same class. Analyzer **CSP041**.

---

## 4. Public API

### 4.1 Class attribute

```csharp
namespace ComposableSettings;

/// <summary>
/// Marks a partial ViewModel that edits a <see cref="Document.SettingsEditingSession{T}"/>
/// draft with preview/commit semantics (Document profile).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SettingsDraftVmAttribute(Type documentType) : Attribute
{
    public Type DocumentType { get; } = documentType;
}
```

### 4.2 Optional document path prefix (section VMs)

```csharp
/// <summary>
/// When set on a [SettingsDraftVm] class, all [SettingsProxy] paths are resolved
/// relative to this dot-path into the document (e.g. "Layout.Terminal").
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SettingsDraftRootAttribute(string path) : Attribute
{
    public string Path { get; } = path;
}
```

### 4.3 Property proxy (extended — shared with `[SettingsVm]`)

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingsProxyAttribute : Attribute
{
    public string? MemberPath { get; }

    public SettingsProxyAttribute() { }

    /// <summary>
    /// Dot-path from document root (or from <see cref="SettingsDraftRootAttribute"/> when present).
    /// Example: "Padding.Horizontal" or "Layout.Terminal.Padding.Horizontal".
    /// </summary>
    public SettingsProxyAttribute(string memberPath) => MemberPath = memberPath;
}
```

**Resolution order for proxy `TerminalPaddingHorizontal`:**

1. `MemberPath` on `[SettingsProxy("Padding.Horizontal")]`
2. Else property name matched against document type at root (or `SettingsDraftRoot` type)

### 4.4 Runtime helpers (Document assembly)

```csharp
namespace ComposableSettings.Document;

/// <summary>Change-equality helpers for draft setters (moved from NC DraftPropertyGuard).</summary>
public static class DraftMutation { ... }

/// <summary>Preview / commit / cancel without MVVM toolkit dependency.</summary>
public static class SettingsDraftCommands { ... }
```

---

## 5. Target developer experience

### 5.1 Full-document VM (settings shell)

```csharp
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsDocumentStore<AppSettings> _store;
    private readonly SettingsEditingSession<AppSettings> _session;

    public SettingsViewModel(ISettingsDocumentStore<AppSettings> store)
    {
        _store = store;
        _session = new SettingsEditingSession<AppSettings>(store.Effective);
        InitializeSettingsDraft(_session, _store);  // generated
    }

    public SettingsEditingSession<AppSettings> Session => _session;

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SettingsDraftCommands.CommitAsync(_session, _store);
    }

    [RelayCommand]
    private void Cancel() => SettingsDraftCommands.Cancel(_session, _store, RefreshAllDraftProxies);
}
```

### 5.2 Section VM (NC-style — main win)

**Before (NC today, ~95 lines for Terminal):**

```csharp
public partial class TerminalSettingsViewModel : SettingsSectionViewModelBase
{
    public double PaddingHorizontal
    {
        get => Terminal.Padding.Horizontal;
        set {
            if (DraftPropertyGuard.SetDouble(..., ApplyPreview))
                OnPropertyChanged();
        }
    }
    // × 6 properties ...
}
```

**After (D5 target):**

```csharp
[SettingsDraftVm(typeof(AppSettings))]
[SettingsDraftRoot("Layout.Terminal")]
public partial class TerminalSettingsViewModel : ObservableObject
{
  public TerminalSettingsViewModel(
      SettingsEditingSession<AppSettings> session,
      ISettingsDocumentStore<AppSettings> store)
  {
      InitializeSettingsDraft(session, store);
  }

  [SettingsProxy("Padding.Horizontal")]
  public partial double PaddingHorizontal { get; set; }

  [SettingsProxy("Padding.Vertical")]
  public partial double PaddingVertical { get; set; }

  [SettingsProxy("Font.Size")]
  public partial double FontSize { get; set; }

  // FontName: custom setter — no [SettingsProxy]
  public string? FontName { get; set; }  // user implements

  partial void ResetDraftSection()
  {
      Session.Draft.Layout.Terminal = FactoryDefaults.Terminal();
  }

  partial void RefreshFromDraft() => RefreshAllDraftProxies();
}
```

Generator emits proxy bodies; user keeps **only** domain-specific pieces.

### 5.3 Custom setter (escape hatch)

```csharp
public string? FontName
{
    get => GetDraftValue<string>("Font.FontName");
    set
    {
        var normalized = LayoutFontSettings.NormalizeFontName(value);
        if (!DraftMutation.TrySet(
                GetDraftValue<string>("Font.FontName"),
                normalized,
                v => SetDraftValue("Font.FontName", v),
                OnDraftPropertyChanged))
            return;
        OnPropertyChanged();
    }
}
```

`GetDraftValue` / `SetDraftValue` / `OnDraftPropertyChanged` — generated helpers for
the class's `SettingsDraftRoot` + path.

---

## 6. Generated code (normative)

For `[SettingsDraftVm(typeof(TDocument))]` partial class deriving from INPC host
(e.g. `ObservableObject`):

### 6.1 Fields & init

```csharp
private SettingsEditingSession<TDocument> _draftSession = null!;
private ISettingsDocumentStore<TDocument> _draftStore = null!;
private bool _draftVmDisposed;

private void InitializeSettingsDraft(
    SettingsEditingSession<TDocument> session,
    ISettingsDocumentStore<TDocument> store)
{
    _draftSession = session;
    _draftStore = store;
    store.EffectiveChanged += OnStoreEffectiveChanged;
}

private TDocument Draft => _draftSession.Draft;
```

### 6.2 Each `[SettingsProxy]` property

```csharp
public partial double PaddingHorizontal
{
    get => Draft.Layout.Terminal.Padding.Horizontal;
    set
    {
        if (!DraftMutation.TrySetDouble(
                Draft.Layout.Terminal.Padding.Horizontal,
                value,
                v => Draft.Layout.Terminal.Padding.Horizontal = v,
                OnDraftPropertyChanged))
            return;
        OnPropertyChanged();
    }
}
```

### 6.3 `OnDraftPropertyChanged` (single funnel)

```csharp
private void OnDraftPropertyChanged()
{
    _draftSession.Touch();
    _draftStore.Preview(_draftSession.Draft);
    OnDraftMemberChanged(_lastChangedPath);  // optional partial hook
}
```

### 6.4 External refresh

```csharp
private void OnStoreEffectiveChanged(object? sender, EventArgs e)
{
    if (_draftVmDisposed) return;
    RefreshAllDraftProxies();  // OnPropertyChanged for each proxy + OnDraftMemberChanged(null)
}

partial void OnDraftMemberChanged(string? memberPath);
partial void RefreshFromDraft();  // user calls NotifyAll or section-specific logic
```

### 6.5 Dispose

```csharp
void DisposeGeneratedSettingsDraft()
{
    if (_draftVmDisposed) return;
    _draftVmDisposed = true;
    _draftStore.EffectiveChanged -= OnStoreEffectiveChanged;
}
```

**Note:** unlike `[SettingsVm]`, draft models are **plain POCO** by default — no
`PropertyChanged` on `Draft`. VM owns INPC for bound properties.

---

## 7. `SettingsDraftCommands` (runtime, no MVVM dependency)

```csharp
public static class SettingsDraftCommands
{
    public static void Preview<T>(SettingsEditingSession<T> session, ISettingsDocumentStore<T> store)
        where T : class, new()
    {
        session.Touch();
        store.Preview(session.Draft);
    }

    public static async Task CommitAsync<T>(
        SettingsEditingSession<T> session,
        ISettingsDocumentStore<T> store,
        CancellationToken ct = default)
        where T : class, new()
    {
        await store.CommitAsync(session.Draft, ct);
        session.UpdateBaseline();
    }

    public static void Cancel<T>(
        SettingsEditingSession<T> session,
        ISettingsDocumentStore<T> store,
        Action refreshUi)
        where T : class, new()
    {
        session.ResetFromBaseline();
        store.Preview(session.Draft);
        refreshUi();
    }

    public static void ResetSection<T>(
        SettingsEditingSession<T> session,
        ISettingsDocumentStore<T> store,
        Action<T> resetSection,
        Action refreshUi)
        where T : class, new()
    {
        resetSection(session.Draft);
        Preview(session, store);
        refreshUi();
    }
}
```

Apps using CommunityToolkit wrap these in `[RelayCommand]` one-liners.

---

## 8. Path resolution (generator)

New helper: `DocumentMemberPathResolver` (mirror `SettingsModelMemberResolver`):

| Input | Example | Resolved accessor |
|-------|---------|-------------------|
| Root + name | `ThemeId` | `Draft.ThemeId` |
| Root + path | `Layout.Terminal.Padding.Horizontal` | `Draft.Layout.Terminal.Padding.Horizontal` |
| Section root `Layout.Terminal` + `Padding.Horizontal` | combined | `Draft.Layout.Terminal.Padding.Horizontal` |

Validation at compile time:

- **CSP042** — path segment not found on document type
- **CSP043** — proxy property type mismatch at leaf

Supports:

- `[SettingsModel]` generated properties (underscore fields)
- Ordinary POCO properties on consumer document types (NC `AppSettings` — no `[SettingsModel]` required on document root)

---

## 9. Analyzers

| ID | Severity | Rule |
|----|----------|------|
| **CSP040** | Error | Same `T` registered with both `AddSettingsProvider<T>` and `AddComposableSettingsDocument<T>` without documented split |
| **CSP041** | Error | `[SettingsVm]` and `[SettingsDraftVm]` on same class |
| **CSP044** | Error | `[SettingsDraftVm]` class not `partial` |
| **CSP045** | Error | `[SettingsDraftVm]` missing document type argument |
| **CSP042** | Error | `[SettingsProxy]` path invalid for document type |
| **CSP043** | Error | `[SettingsProxy]` type mismatch at path leaf |
| **CSP046** | Warning | `[SettingsProxy]` on class without `[SettingsVm]` or `[SettingsDraftVm]` |

CSP040 may ship in D5 or D6 — documented here for completeness.

---

## 10. Implementation phases

| Sub-phase | Deliverable | Unblocks |
|-----------|-------------|----------|
| **D5a** | `SettingsDraftVmAttribute`, `SettingsDraftRootAttribute`, extend `SettingsProxyAttribute`, `DraftMutation`, `SettingsDraftCommands` | runtime API frozen |
| **D5b** | `DocumentMemberPathResolver` + unit tests in Generator.Tests | path validation |
| **D5c** | `SettingsDraftVmGenerator` — flat + nested paths, `InitializeSettingsDraft`, proxies, dispose | first consumer VM |
| **D5d** | Analyzers CSP041, CSP042, CSP043, CSP044, CSP045 | safe adoption |
| **D5e** | NC pilot: `TerminalSettingsViewModel` + doc in NC repo | prove ROI |

**Estimated generator LOC:** ~350–450 (symmetric to `ObservableSettingsGenerator`).

---

## 11. NanoCommander migration map (D5 + D6)

### Infrastructure (D6 — store)

| NC today | CS |
|----------|-----|
| `SettingsStore` | `ISettingsDocumentStore<AppSettings>` |
| `ThemePackService.ApplyPackLayer` | `AddComposableSettingsLayering` + `AddComposableSettingsPacks` |
| `SettingsEditingSession` (NC VM) | `ComposableSettings.Document.SettingsEditingSession<T>` |
| `settings.json` full effective | user layer + migrator |

### ViewModels (D5 — generator)

| NC today | After D5 |
|----------|----------|
| `SettingsSectionViewModelBase` | `SettingsDraftCommands` + optional thin app base |
| `DraftPropertyGuard` | `DraftMutation` (CS) |
| Manual proxy properties | `[SettingsProxy("…")]` partials |
| `ApplyPreview()` in every setter | generated `OnDraftPropertyChanged` |

### Stays in NC

- `SettingsViewModel` section navigation
- `ThemeSettingsViewModel` (pack export, catalog)
- `ShaderSettingsViewModel` (`IOverlayShaderCatalog`)
- `SettingsDraftDefaults` / factory slices per section
- `ResetSectionDefaults` bodies

### Rough LOC impact (NC ViewModels/Settings glue)

| Area | Lines removed (est.) |
|------|-------------------|
| DraftPropertyGuard + repetitive setters | ~800–1200 |
| SettingsSectionViewModelBase | ~50 (replaced by commands) |
| SettingsEditingSession duplicate | ~40 |
| **Net VM savings** | **~900–1300** |

Plus ~400–600 lines from D6 store/merge/pack infra in `NanoCommander.Settings`.

---

## 12. Multi-app adoption checklist

For each new rich-settings MVVM app:

1. **Document model** — one (or few) root POCO types, JSON on disk  
2. **DI** — `AddComposableSettingsDocument<T>` (+ layering/packs if needed)  
3. **Settings window** — create `SettingsEditingSession<T>(store.Effective)`  
4. **Section VMs** — `[SettingsDraftVm]` + `[SettingsDraftRoot]` + `[SettingsProxy]`  
5. **Save/Cancel** — `SettingsDraftCommands.CommitAsync` / `Cancel`  
6. **Main window** — inject `ISettingsDocumentStore<T>.Effective` (not session draft)

**Do not** use `[SettingsVm]` for editor-bound slices on the same document file.

---

## 13. Resolved design decisions

| Question | Decision |
|----------|----------|
| INPC on document model? | **No** — draft is POCO; VM relays INPC (simpler, works with existing NC models) |
| Nested paths? | **Yes** — `[SettingsProxy("A.B.C")]` + optional `[SettingsDraftRoot]` |
| Preview on every setter? | **Yes** — matches NC; debounce stays on `CommitAsync` in store |
| Base class with RelayCommand? | **No** in CS core — `SettingsDraftCommands` static helpers only |
| Share `[SettingsProxy]` with `[SettingsVm]`? | **Yes** — same attribute; generator picks behavior by class attribute |
| `[SettingsModel]` required on document? | **No** — resolver reads real properties or `[SettingsModel]` fields |

---

## 14. Example — minimal new app

```csharp
// AppSettings.cs — plain POCO
public class AppSettings
{
    public string Theme { get; set; } = "light";
    public EditorSettings Editor { get; set; } = new();
}

// EditorSettingsViewModel.cs
[SettingsDraftVm(typeof(AppSettings))]
[SettingsDraftRoot("Editor")]
public partial class EditorSettingsViewModel : ObservableObject
{
    public EditorSettingsViewModel(
        SettingsEditingSession<AppSettings> session,
        ISettingsDocumentStore<AppSettings> store)
        => InitializeSettingsDraft(session, store);

    [SettingsProxy("FontSize")]
    public partial double FontSize { get; set; }

    [SettingsProxy("ShowLineNumbers")]
    public partial bool ShowLineNumbers { get; set; }
}
```

---

## 15. Success criteria

D5 is **done** when:

1. Generator tests cover flat + nested proxies, dispose, `EffectiveChanged` refresh  
2. NC `TerminalSettingsViewModel` pilot compiles with ≥50% fewer lines  
3. README documents `[SettingsDraftVm]` alongside `[SettingsVm]`  
4. A second app (or Actuator settings editor spike) can adopt without NC-specific types  

---

*Spec v1.0 — `[SettingsDraftVm]` as Document-profile sibling of `[SettingsVm]`.*
