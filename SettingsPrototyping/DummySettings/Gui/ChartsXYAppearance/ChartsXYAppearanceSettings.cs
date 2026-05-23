namespace ComposableSettings.DummySettings.Gui.ChartsXYAppearance;

public class ChartsXYAppearanceSettings
{
    public Chart2DRenderingMode RenderingMode { get; init; } =
        Chart2DRenderingMode.ActualSamples;

    public double ApproximateMorphDurationSeconds { get; init; } = 0.15;

    public string GridLineColor { get; init; } = "#223247";

    public string AxisLineColor { get; init; } = "#6D7C98";

    public string SeriesColor { get; init; } = "#FF8A65";

    public ChartGapsSettings Gaps { get; init; } = new();
}