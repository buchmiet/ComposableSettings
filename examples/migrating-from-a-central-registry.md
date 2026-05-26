# Migrating from a central settings registry

A real-world case study: moving a desktop app — **Actuator** (an Avalonia GUI that
hosts a job-runtime in-process) — from a hand-wired central settings layer to
ComposableSettings. The same pattern applies to any app whose settings have grown
into a "touch many files to add one setting" registry.

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

### 2. Each owner registers its own file

The host owns `runtime.xml`; the GUI owns `gui.xml`. Neither references the other.

```csharp
// host composition root
services.AddComposableSettingsFile("runtime", runtimeXmlPath);
services.AddSettingsProvider<RuntimeSettings>("runtime", SettingsNodePath.Root("runtime"));

// gui composition root (added only when the GUI is hosted)
services.AddComposableSettingsFile("gui", guiXmlPath);
services.AddSettingsProvider<ClockAppearanceSettings>("gui", SettingsNodePath.Root("clock"));
services.AddSettingsProvider<Chart2DSettings>("gui", SettingsNodePath.Root("chart2d"));
// ...one per node
```

This makes the GUI ⇄ tray switch safe: each process opens the file(s) it owns; the
other owner's file is untouched.

### 3. The engine becomes a plain consumer

```csharp
// before: engine owns RuntimePreferencesService + GetPreferences()/Update*Async ladder
// after:
public sealed class ActuatorEngine(ISettingsProvider<RuntimeSettings> settings)
{
    // read settings.Current.*; subscribe to changes if needed — no snapshot, no Update*Async
}
```

### 4. The UI binds directly

```csharp
[SettingsConsumer(typeof(ClockAppearanceSettings))]
public partial class ClockSettingsViewModel
{
    public ClockSettingsViewModel(ISettingsProvider<ClockAppearanceSettings> settings)
        => InitializeGeneratedSettings(settings);
}
```

```xml
<Slider Value="{Binding Settings.Glow.GlowIntensity, Mode=TwoWay}" />
```

Editing the slider persists immediately (deep change → `PropertyChanged` →
provider auto-persists). `LoadPreferences` / `ApplyXSettings` /
`UpdateXAsync` all disappear.

## Suggested order (incremental, each step builds & ships)

1. Add `PackageReference Include="ComposableSettings"`; remove any in-repo settings
   adapter/registry.
2. Migrate **one** screen (e.g. Clock): model → `[SettingsModel]`, VM →
   `[SettingsConsumer]`, register the `gui` file + node, bind directly. Verify
   edit → persist → reload.
3. Migrate the remaining screens one by one, deleting their `Update*Async` methods
   and path constants as you go.
4. Migrate the runtime owner: a `runtime.xml` file + `ISettingsProvider<RuntimeSettings>`;
   the engine reads from the provider instead of the central state. Schedules /
   packages become `ObservableCollection<...>` of `[SettingsModel]` items.
5. Delete the central layer: the preferences service, the aggregate snapshot
   record, the engine/control `Update*Async` ladder, the path constants.

## What stays in the runtime snapshot

Keep **derived/operational** state (e.g. the *effective* job schedule shown in the
UI, run status, notifications) in your existing push-snapshot. Only **persisted
intent** (the schedule override the user set) moves into settings. Settings are
trustworthy state at rest; the snapshot is a derived view.
