namespace ComposableSettings.DummySettings.Gui.ConfigfurationButton;

public class ConfigurationButtonSettings
{
    public PresentedGlyph Layout { get; init; } =
        PresentedGlyph.HorizontalRoundSliders;

    public bool AlertUseHdr { get; init; } = true;

    public int AlertHdrIntensity { get; init; } = 80;

    public int AlertBlinkRate { get; init; } = 3;
}