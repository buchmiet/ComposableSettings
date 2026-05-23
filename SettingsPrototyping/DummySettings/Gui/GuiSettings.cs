using ComposableSettings.DummySettings.Gui.ChartsXYAppearance;
using ComposableSettings.DummySettings.Gui.ClockAppearance;
using ComposableSettings.DummySettings.Gui.ClockOnlyAppearance;
using ComposableSettings.DummySettings.Gui.ConfigfurationButton;
using ComposableSettings.DummySettings.Gui.DistinctColors;
using ComposableSettings.DummySettings.Gui.JobsAppearance;
using ComposableSettings.DummySettings.Gui.LogPanelAppearance;

namespace ComposableSettings.DummySettings.Gui;

public class GuiSettings
{
    public ConfigurationButtonSettings ConfigurationButtonSettings { get; init; } = new();
    public JobsAppearanceSettings Jobs { get; init; } = new();

    public ClockAppearanceSettings Clock { get; init; } = new();

    public ChartsXYAppearanceSettings Chart2D { get; init; } = new();

    public ColorPaletteSettings ColorPalette { get; init; } = new();

    public ClockOnlyAppearanceSettings ClockOnlyMode { get; init; } = new();

    public LogLineRenderSettings LogLineRender { get; init; } = new();
}