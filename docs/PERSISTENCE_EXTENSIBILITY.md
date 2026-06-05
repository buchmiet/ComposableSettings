# Persistence extensibility (design note)

> **Status: design intent / forward-compatibility guide.** Records how the persistence
> layer is meant to evolve so future changes stay **additive** and never break the current API.

ComposableSettings keeps the live settings model (INPC, via `[SettingsModel]` +
`ISettingsProvider<T>` auto-persist) **decoupled** from how bytes reach disk. Persistence
has two independent axes.

## 1. Format (codec) — already swappable

The storage seam is **`IComponentSettingsProvider`** (`Get<TSettings>` / `Set<TSettings>`).
Each file format is just an implementation; DI chooses which:

- `XmlSettingsFile` — XML (current default)
- `JsonSettingsFile` — JSON (System.Text.Json)
- *(future)* `YamlSettingsFile`, etc.

No API change is needed to add a format — implement the interface and register it per file/key.

## 2. Write strategy — planned extension point (NOT yet built)

Today `Set<TSettings>(path, value)` is **whole-object**: the provider serializes the entire
settings object for a node and the file impl rewrites it (full rewrite). This is correct and
cheap for typical/small settings files.

For **large or high-write** settings (hundreds of values, frequent writes), a **surgical /
byte-indexed** write strategy — patch a single changed value without rewriting the whole file —
is desirable. A working byte-offset indexer prototype exists (the `jsonsettings` experiment).

**This is a FUTURE, ADDITIVE extension — not an impl swap and not a breaking change:**

- The current whole-object API stays as-is and keeps working (it is the default).
- Surgical writes arrive as an **opt-in granular write seam** (e.g. a new optional interface /
  overload carrying the changed property path), selectable in DI.
- `SettingsProvider` already receives the changed property name from `INotifyPropertyChanged`,
  so it can drive a granular write **without** changing the model or the whole-object path.

## Guidance for future work

- Do **not** break or replace `IComponentSettingsProvider.Set<TSettings>(path, value)`.
- Add surgical / indexed writes as an **additive**, opt-in capability; keep full-rewrite the default.
- Preserve the `jsonsettings` indexer/codec code as the basis for the alpha write strategy.
