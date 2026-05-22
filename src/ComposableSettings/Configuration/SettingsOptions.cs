namespace ComposableSettings.Configuration;

public class SettingsOptions
{
    public required string AppName { get; init; }
    
    public PersistenceType PersistenceType { get; init; }
}