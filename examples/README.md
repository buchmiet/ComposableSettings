# Examples

- **[Migrating from a central settings registry](migrating-from-a-central-registry.md)**
  — a real-world, step-by-step migration of a desktop app ("Actuator") away from a
  hand-wired central settings layer (a preferences service + an aggregate snapshot
  DTO + per-type `UpdateXAsync` methods + path constants) to ComposableSettings
  (per-owner files, `[SettingsModel]` models, `ISettingsProvider<T>`, direct binding).

For the core API and a from-scratch quick start, see the
[package README](../README.md).
