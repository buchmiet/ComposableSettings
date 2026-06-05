# Migrating from a central settings registry

A real-world case study: moving a desktop app — **Actuator** (an Avalonia GUI that
hosts a job-runtime in-process) — from a hand-wired central settings layer to
ComposableSettings. The same pattern applies to any app whose settings have grown
into a "touch many files to add one setting" registry.

> **As of 2026-06:** clock appearance uses **`clock.json`** (JSON file + `JsonSettingsFile`);
> GUI alert/HDR uses **`gui.xml`** node `alert`; editor VMs use **`[SettingsVm]`** (not
> `[SettingsConsumer]`). See actuator `SETTINGS_DECISIONS_LOG.md` (D10, D4/D5).

## Before: the central registry

Adding **one** setting touched ~6 places:

1. an aggregate snapshot record exposed to the UI:

   ```csharp
   public record ActuatorRuntimePreferencesSnapshot(
       string? LastPluginBrowseDirectory,
       EmitSettings Emit, NotifySettings Notify, ClockAppearanceSettings Clock,
       ClockOnlyModeSettings ClockOnlyMode, Chart2DSettings Chart2D,
       ColorPaletteSettings ColorPalette, GuiSettings Gui, LogRenderSettings LogRender);
   ```

2. a preferences service assembling it and writing each type:

   ```csharp
   public Task<...> UpdateClockAppearanceAsync(ClockAppearanceSettings clock, ...)
   {
       lock (_gate) { _persistedState.Gui.Appearance.Clock = clock; }
       _settingsFile.Set(KnownSettingsPaths.Clock, clock);   // path constant
       return Task.FromResult(GetPreferences());
   }
   // ...one Update*Async per settings type (x8)
   ```

3. a matching `Update*Async` on the engine, **4.** another on the control facade,
   **5.** a `KnownSettingsPaths.X` constant, **6.** a per-type DI registration.

Plus two parallel files (`settings.xml` and `gui.xml`) and a UI that pulled a whole
snapshot, then pushed changes back via the ladder
(`LoadPreferences` / `ApplyClockSettings` / `UpdateClockAppearanceAsync`).

## After: ComposableSettings

### 1. Settings become observable models

`init`-only DTOs become `[SettingsModel] partial` classes with `_camelCase` fields
(`List<>` → `ObservableCollection<>`, nested objects as non-`readonly` fields):

```csharp
// before
public class ClockAppearanceSettings
{
    public string BaseColor { get; init; } = "#e6194b";
    public GlowConfiguration Glow { get; init; } = new();
    // ...
}

// after
[SettingsModel]
public partial class ClockAppearanceSettings
{
    private string _baseColor = "#e6194b";
    private GlowConfiguration _glow = new();   // GlowConfiguration is also [SettingsModel]
    // ...
}
```

### 2. Each slice registers its own file (or node)

Per-owner files stay separate (`runtime.xml` vs GUI files). Slices can also use
**their own JSON file** when a whole document is simpler than an XML node:

```csharp
// host composition root
services.AddComposableSettingsFile("runtime", runtimeXmlPath);
services.AddSettingsProvider<RuntimeSettings>("runtime", SettingsNodePath.Root("runtime"));

// gui — clock slice: dedicated JSON file (D10)
services.AddComposableSettingsJsonFile("clock", clockJsonPath);
services.AddSettingsProvider<ClockAppearanceSettings>("clock", SettingsNodePath.Root("clock"), TimeSpan.FromMilliseconds(250));

// gui — alert slice: still in gui.xml under alert node
services.AddComposableSettingsFile("gui", guiXmlPath);
services.AddSettingsProvider<GuiAlertSettings>("gui", SettingsNodePath.Root("alert"), TimeSpan.FromMilliseconds(250));
```

This keeps GUI ⇄ tray switch safe: each process opens the file(s) it owns.

### 3. The engine becomes a plain consumer

```csharp
// before: engine owns RuntimePreferencesService + GetPreferences()/Update*Async ladder
// after:
public sealed class ActuatorEngine(ISettingsProvider<RuntimeSettings> settings)
{
    // read settings.Current.*; subscribe to changes if needed — no snapshot, no Update*Async
}
```

### 4. The UI binds with `[SettingsVm]` (MVVM)

Use **`[SettingsVm]`** when the ViewModel already derives from `ObservableObject`.
Use **`[SettingsConsumer]`** only for plain partial classes without INPC.

```csharp
[SettingsVm(typeof(ClockAppearanceSettings))]
public partial class ClockSettingsViewModel : ObservableObject, IDisposable
{
    public ClockSettingsViewModel(ISettingsProvider<ClockAppearanceSettings> settings)
        => InitializeSettings(settings);

    [SettingsProxy] public partial bool IsGlslEnabled { get; set; }

    public void Dispose() => DisposeGeneratedSettings();
}
```

```xml
<Slider Value="{Binding Settings.Glow.GlowIntensity, Mode=TwoWay}" />
```

Editing the slider persists immediately (deep change → `PropertyChanged` →
provider auto-persists). `LoadPreferences` / `ApplyXSettings` /
`UpdateXAsync` disappear for migrated slices.

**Dashboard consuming two slices:** `ClockViewModel` uses `[SettingsVm]` for
appearance and a **manual relay** for `GuiAlertSettings` (one `[SettingsVm]` per class).
See actuator `SETTINGS_CLOCK_VM_REFACTOR_ALPHA.md`.

## Suggested order (incremental, each step builds & ships)

1. Add `PackageReference Include="ComposableSettings" Version="1.0.*"`; remove any in-repo
   settings adapter/registry.
2. Migrate **one vertical slice** (e.g. Clock): model → `[SettingsModel]`, editor VM →
   `[SettingsVm]`, register file + provider, bind directly. Verify edit → persist → reload.
3. Migrate remaining screens one by one; delete their `Update*Async` methods and path
   constants as each slice lands.
4. Migrate the runtime owner: `runtime.xml` + `ISettingsProvider<RuntimeSettings>`;
   the engine reads from the provider. Schedules / packages → `ObservableCollection<...>`
   of `[SettingsModel]` items.
5. Delete the central layer: preferences service, aggregate snapshot, `Update*Async`
   ladder, path constants.

## What stays in the runtime snapshot

Keep **derived/operational** state (e.g. the *effective* job schedule shown in the
UI, run status, notifications) in your existing push-snapshot. Only **persisted
intent** (the schedule override the user set) moves into settings. Settings are
trustworthy state at rest; the snapshot is a derived view.

## Related

- [Package README](../README.md) — API reference
- [Persistence extensibility](../docs/PERSISTENCE_EXTENSIBILITY.md) — format vs write strategy (D11)
