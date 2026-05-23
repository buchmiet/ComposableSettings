namespace ComposableSettings.DummySettings.Gui.ChartsXYAppearance;

public class ChartGapsSettings
{
    public ChartXYGapMode Mode { get; init; } = ChartXYGapMode.Bands;

    public string Color { get; init; } = "#FFC107";

    public bool ShowBandEdges { get; init; } = true;
}