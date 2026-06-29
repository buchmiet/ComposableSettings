using ComposableSettings.Configuration;

namespace ComposableSettings.Static;

public static class SettingsPathResolver
{
    private static string ResolveDirectory(SettingsFileOptions options)
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

    private static string ResolveAppFolderName(SettingsFileOptions options, bool useSystemDirectory)
    {
        return useSystemDirectory && OperatingSystem.IsLinux()
            ? options.AppName.ToLowerInvariant()
            : options.AppName;
    }

    private static string ResolveSystemBaseDirectory(SettingsFileOptions options)
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        if (OperatingSystem.IsMacOS())
            return Path.Combine(Path.DirectorySeparatorChar.ToString(), "Library", "Application Support");

        // Linux / Unix: XDG config (~/.config on .NET via ApplicationData).
        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }

    public static string ResolveFilePath(SettingsFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.FileName))
            throw new ArgumentException("FileName is required.", nameof(options));

        var fileName = HasExplicitExtension(options.FileName)
            ? options.FileName
            : options.FileName + ".xml";

        return Path.Combine(ResolveDirectory(options), fileName);
    }

    public static string ResolveJsonFilePath(SettingsFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.FileName))
            throw new ArgumentException("FileName is required.", nameof(options));

        var fileName = HasExplicitExtension(options.FileName)
            ? options.FileName
            : options.FileName + ".json";

        return Path.Combine(ResolveDirectory(options), fileName);
    }

    private static bool HasExplicitExtension(string fileName)
        => fileName.Contains('.', StringComparison.Ordinal);
}