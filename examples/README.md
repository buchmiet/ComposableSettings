# Examples

- **[Migrating from a central settings registry](migrating-from-a-central-registry.md)**
  — a real-world, step-by-step migration of a desktop app ("Actuator") away from a
  hand-wired central settings layer (a preferences service + an aggregate snapshot
  DTO + per-type `UpdateXAsync` methods + path constants) to ComposableSettings
  (per-owner files, `[SettingsModel]` models, `ISettingsProvider<T>`, direct binding).

For the core API and a from-scratch quick start, see the
[package README](../README.md).

For persistence evolution (codec vs write strategy), see
[docs/PERSISTENCE_EXTENSIBILITY.md](../docs/PERSISTENCE_EXTENSIBILITY.md).

For rich single-file apps (theme packs, preview/commit, layered defaults) — the
**Document profile** (proposed, same NuGet, opt-in DI modules), see
[docs/DOCUMENT-SETTINGS-PROFILE.md](../docs/DOCUMENT-SETTINGS-PROFILE.md).
