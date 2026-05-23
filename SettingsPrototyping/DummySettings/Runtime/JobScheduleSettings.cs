namespace ComposableSettings.DummySettings.Runtime;

public class JobScheduleSettings
{
    public string JobId { get; init; } = string.Empty;

    public string CronExpression { get; init; } = string.Empty;
}