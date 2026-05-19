namespace ComposableSettings.Xml;

public static class ComponentSettingsPathResolver
{
    private static string ResolveDirectory(ComponentSettingsFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.AppName))
            throw new ArgumentException("AppName is required.", nameof(options));

        var baseDirectory = string.IsNullOrWhiteSpace(options.BaseDirectoryOverride)
            ? ResolveUserBaseDirectory()
            : options.BaseDirectoryOverride;

        var folderName = string.IsNullOrWhiteSpace(options.FolderName)
            ? options.AppName
            : options.FolderName;

        return Path.Combine(baseDirectory, folderName);
    }

    public static string ResolveFilePath(ComponentSettingsFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.FileName))
            throw new ArgumentException("FileName is required.", nameof(options));

        var fileName = options.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            ? options.FileName
            : options.FileName + ".xml";

        return Path.Combine(ResolveDirectory(options), fileName);
    }

    private static string ResolveUserBaseDirectory()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Application Support")
            : Path.Combine(home, ".config");
    }
}
