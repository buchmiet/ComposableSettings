namespace ComposableSettings.Configuration;

public class SettingsFileOptions:SettingsOptions
{
    public required string FileName { get; init; }
    public string? FolderName { get; init; }

    /// <summary>
    ///     Overrides the platform-default base directory (user's AppData / ~/.config).
    ///     Use this to target machine-wide or custom storage locations.
    /// </summary>
    public string? BaseDirectoryOverride { get; init; }
}