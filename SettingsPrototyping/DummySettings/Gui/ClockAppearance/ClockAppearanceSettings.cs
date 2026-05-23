using ComposableSettings.DummySettings.Gui.ClockAppearance.PostProcessingEffects;

namespace ComposableSettings.DummySettings.Gui.ClockAppearance;

public class ClockAppearanceSettings
{
    public bool IsGlslEnabled { get; init; } = true;

    public ClockPostProcessStyle SelectedEffect { get; init; } = ClockPostProcessStyle.ExperimentalGlsl;

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