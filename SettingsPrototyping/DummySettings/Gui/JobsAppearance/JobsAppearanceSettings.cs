namespace ComposableSettings.DummySettings.Gui.JobsAppearance;

public class JobsAppearanceSettings
{
    public EmitConfiguration Emit { get; init; } = new();

    public NotifyConfiguration Notify { get; init; } = new();
}