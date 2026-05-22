using System.Xml.Serialization;

namespace ComposableSettings;


public class RuntimeConfiguration
{
    public string PluginsFolder { get; init; } =
        @"C:\sandbox\actuator\artifacts\plugins";

    public IReadOnlyList<PackageConfiguration> Packages { get; init; } =
    [
        new() { Id = "Plugin.EventMirror", State = PackageState.Installed },
        new() { Id = "Plugin.IntegrationScenarios", State = PackageState.Installed },
        new() { Id = "Plugin.Pulse", State = PackageState.Installed },
        new() { Id = "Plugin.RandomWalk", State = PackageState.Installed },
        new() { Id = "Plugin.SecretAudit", State = PackageState.Installed }
    ];

    public IReadOnlyList<JobScheduleConfiguration> JobSchedules { get; init; } =
    [
        new() { JobId = "process-info", Cron = "0/10 * * * * ?" }
    ];
}

public class PackageConfiguration
{
    public string Id { get; init; } = string.Empty;

    public PackageState State { get; init; } = PackageState.Installed;
}

public enum PackageState
{
    Installed
}

public class JobScheduleConfiguration
{
    public string JobId { get; init; } = string.Empty;

    public string Cron { get; init; } = string.Empty;
}

public class GuiConfiguration
{
    public AppearanceConfiguration Appearance { get; init; } = new();

    public GuiSettingsConfiguration Settings { get; init; } = new();
}

public class AppearanceConfiguration
{
    public JobsAppearanceConfiguration Jobs { get; init; } = new();

    public ClockConfiguration Clock { get; init; } = new();

    public Chart2DConfiguration Chart2D { get; init; } = new();

    public ColorPaletteConfiguration ColorPalette { get; init; } = new();

    public ClockOnlyModeConfiguration ClockOnlyMode { get; init; } = new();

    public LogRenderConfiguration LogRender { get; init; } = new();
}

public class JobsAppearanceConfiguration
{
    public EmitConfiguration Emit { get; init; } = new();

    public NotifyConfiguration Notify { get; init; } = new();
}

public class EmitConfiguration
{
    public int LumSteps { get; init; } = 6;

    public int LightLength { get; init; } = 4;

    public int GapLength { get; init; } = 6;

    public int WaitTime { get; init; } = 150;

    public int BrightnessAmount { get; init; } = 95;

    public int GlowMultiplier { get; init; } = 25;

    public int ControlOffset { get; init; } = 2;

    public bool ExperimentalBackgroundWave { get; init; } = false;
}

public class NotifyConfiguration
{
    public int StaggerMs { get; init; } = 200;

    public int SpringVelocity { get; init; } = 160;

    public int SpringStiffness { get; init; } = 180;

    public int SpringDamping { get; init; } = 83;

    public int ScalePower { get; init; } = 8;

    public int LiftPower { get; init; } = 18;

    public int RotatePower { get; init; } = 51;

    public int NotifyGlow { get; init; } = 7;

    public int PingBounces { get; init; } = 8;

    public int PingDecay { get; init; } = 22;

    public int PingDuration { get; init; } = 1150;
}

public class ClockConfiguration
{
    public bool IsGlslEnabled { get; init; } = true;

    public ClockEffect SelectedEffect { get; init; } = ClockEffect.ExperimentalGlsl;

    public string BaseColor { get; init; } = "#e6194b";

    public GlowConfiguration Glow { get; init; } = new();

    public RgbSplitConfiguration RgbSplit { get; init; } = new();

    public ProjectCrtConfiguration ProjectCrt { get; init; } = new();

    public BloomConfiguration Bloom { get; init; } = new();

    public DitherConfiguration Dither { get; init; } = new();

    public NegativeConfiguration Negative { get; init; } = new();

    public SpotlightConfiguration Spotlight { get; init; } = new();

    public GlitchConfiguration Glitch { get; init; } = new();
}

public enum ClockEffect
{
    ExperimentalGlsl
}

public class GlowConfiguration
{
    public int WaveFrequency { get; init; } = 24;

    public double WaveAmplitude { get; init; } = 1.35;

    public double ColorShiftAmount { get; init; } = 1.75;

    public double GlowIntensity { get; init; } = 0.18;
}

public class RgbSplitConfiguration
{
    public double SplitAmount { get; init; } = 1.1;

    public double WobbleSpeed { get; init; } = 9.42;

    public double WobbleAmount { get; init; } = 0.42;
}

public class ProjectCrtConfiguration
{
    public double CurvatureX { get; init; } = 0.38;

    public double CurvatureY { get; init; } = 0.3;

    public double ScanlineIntensity { get; init; } = 0.03;

    public double RgbSplit { get; init; } = 1.1;
}

public class BloomConfiguration
{
    public double BlurRadius { get; init; } = 1.4;

    public double BloomIntensity { get; init; } = 0.58;
}

public class DitherConfiguration
{
    public int ColorLevels { get; init; } = 3;
}

public class NegativeConfiguration
{
    public double ThresholdMin { get; init; } = 0.05;

    public double ThresholdMax { get; init; } = 0.24;
}

public class SpotlightConfiguration
{
    public double SpotlightRadius { get; init; } = 0.36;

    public double AmbientLight { get; init; } = 0.52;

    public double LightSpeed { get; init; } = 3;
}

public class GlitchConfiguration
{
    public double GlitchSpeed { get; init; } = 1.7;

    public double DistortionAmount { get; init; } = 0.085;

    public double TearAmount { get; init; } = 0.14;
}

public class Chart2DConfiguration
{
    public ChartRenderingMode RenderingMode { get; init; } =
        ChartRenderingMode.ActualSamples;

    public double ApproximateMorphDurationSeconds { get; init; } = 0.15;

    public string GridLineColor { get; init; } = "#223247";

    public string AxisLineColor { get; init; } = "#6D7C98";

    public string SeriesColor { get; init; } = "#FF8A65";

    public ChartGapsConfiguration Gaps { get; init; } = new();
}

public enum ChartRenderingMode
{
    ActualSamples
}

public class ChartGapsConfiguration
{
    public ChartGapsMode Mode { get; init; } = ChartGapsMode.Bands;

    public string Color { get; init; } = "#FFC107";

    public bool ShowBandEdges { get; init; } = true;
}

public enum ChartGapsMode
{
    Bands
}

public class ColorPaletteConfiguration
{
    public IReadOnlyList<string> Colors { get; init; } =
    [
        "#e6194b",
        "#3cb44b",
        "#ffe119",
        "#4363d8",
        "#f58231",
        "#911eb4",
        "#46f0f0",
        "#f032e6",
        "#bcf60c",
        "#fabebe",
        "#008080",
        "#e6beff",
        "#9a6324",
        "#fffac8",
        "#800000",
        "#aaffc3",
        "#808000",
        "#ffd8b1",
        "#000075",
        "#808080",
        "#ffffff"
    ];
}

public class ClockOnlyModeConfiguration
{
    public bool IsAlwaysOnTop { get; init; } = true;
}

public class LogRenderConfiguration
{
    public int FontSize { get; init; } = 10;

    public double LineThickness { get; init; } = 0.1;

    public int RowPadding { get; init; } = 5;

    public int TimePadding { get; init; } = 5;

    public int Gap { get; init; } = 8;

    public int LineHeight { get; init; } = 10;
}

public class GuiSettingsConfiguration
{
    public SettingsLayout Layout { get; init; } =
        SettingsLayout.HorizontalRoundSliders;

    public bool AlertUseHdr { get; init; } = true;

    public int AlertHdrIntensity { get; init; } = 80;

    public int AlertBlinkRate { get; init; } = 3;
}

public enum SettingsLayout
{
    HorizontalRoundSliders
}
