namespace ComposableSettings.Stores;

/// <summary>Writes text to a file atomically via a temporary sibling file.</summary>
internal static class AtomicFileWrite
{
    public static void WriteAllText(string targetPath, string contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = targetPath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, contents);
            if (File.Exists(targetPath))
                File.Move(tempPath, targetPath, overwrite: true);
            else
                File.Move(tempPath, targetPath);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }

            throw;
        }
    }
}
