namespace ComposableSettings.Xml;

public static class ComponentSettingsPathResolver
{
    private static string ResolveDirectory(ComponentSettingsFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.AppName))
            throw new ArgumentException("AppName is required.", nameof(options));

        var useSystemDirectory = string.IsNullOrWhiteSpace(options.BaseDirectoryOverride);

        var baseDirectory = useSystemDirectory
            ? ResolveSystemBaseDirectory(options)
            : options.BaseDirectoryOverride;

        var appFolderName = ResolveAppFolderName(options, useSystemDirectory);

        return string.IsNullOrWhiteSpace(options.FolderName)
            ? Path.Combine(baseDirectory, appFolderName)
            : Path.Combine(baseDirectory, appFolderName, options.FolderName);
    }

    private static string ResolveAppFolderName(ComponentSettingsFileOptions options, bool useSystemDirectory)
    {
        return useSystemDirectory && OperatingSystem.IsLinux()
            ? options.AppName.ToLowerInvariant()
            : options.AppName;
    }

    private static string ResolveSystemBaseDirectory(ComponentSettingsFileOptions options)
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        return OperatingSystem.IsMacOS()
            ? Path.Combine(Path.DirectorySeparatorChar.ToString(), "Library", "Application Support")
            : Path.Combine(Path.DirectorySeparatorChar.ToString(), "etc");
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
}