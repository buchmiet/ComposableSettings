namespace ComposableSettings.DummySettings.Runtime;

public class RuntimeSettings
{
    public string PluginsFolder { get; init; } = ".\\plugins";
    public List<PluginSettings> Packages { get; init; } = [];
    public List<JobScheduleSettings> JobSchedules { get; init; } =
    [];
}


//buchmiet/ComposableSet