namespace ComposableSettings.DummySettings.Gui.JobsAppearance;

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