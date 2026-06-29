using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ComposableSettings.Packs;

internal static class SettingsPackArchive
{
    internal const string StampFileName = ".source-stamp";

    internal static bool TryNormalizeEntryPath(string entryName, out string relativePath)
    {
        relativePath = "";
        if (string.IsNullOrWhiteSpace(entryName))
            return false;

        var normalized = entryName.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];

        normalized = normalized.TrimStart('/');

        if (string.IsNullOrEmpty(normalized))
            return false;

        if (entryName.Contains(':', StringComparison.Ordinal)
            || entryName.StartsWith('/')
            || entryName.StartsWith('\\'))
        {
            return false;
        }

        if (normalized.Contains(':', StringComparison.Ordinal))
            return false;

        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
                return false;
        }

        relativePath = normalized;
        return true;
    }

    internal static string ComputeSourceStamp(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            var newest = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .DefaultIfEmpty(new FileInfo(sourcePath))
                .Max(info => info.LastWriteTimeUtc.Ticks);
            return $"dir:{newest:x}";
        }

        using var stream = File.OpenRead(sourcePath);
        var hash = SHA256.HashData(stream);
        return $"zip:{Convert.ToHexString(hash)}";
    }

    internal static bool IsCacheFresh(string cacheDirectory, string expectedStamp)
    {
        var stampPath = Path.Combine(cacheDirectory, StampFileName);
        if (!Directory.Exists(cacheDirectory) || !File.Exists(stampPath))
            return false;

        var stamp = File.ReadAllText(stampPath, Encoding.UTF8).Trim();
        return string.Equals(stamp, expectedStamp, StringComparison.Ordinal);
    }

    internal static void WriteStamp(string cacheDirectory, string stamp)
    {
        Directory.CreateDirectory(cacheDirectory);
        File.WriteAllText(Path.Combine(cacheDirectory, StampFileName), stamp, Encoding.UTF8);
    }

    internal static void ExtractZip(string zipPath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destinationRoot = Path.GetFullPath(destinationDirectory);

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (!TryNormalizeEntryPath(entry.FullName, out var relativePath))
                continue;

            var targetPath = Path.GetFullPath(Path.Combine(
                destinationRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

            if (!IsUnderRoot(destinationRoot, targetPath))
                continue;

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            var parent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }

    internal static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var sourceRoot = Path.GetFullPath(sourceDirectory);
        var destinationRoot = Path.GetFullPath(destinationDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            if (relative.Split(Path.DirectorySeparatorChar).Any(segment => segment == ".."))
                continue;

            var target = Path.GetFullPath(Path.Combine(destinationRoot, relative));
            if (!IsUnderRoot(destinationRoot, target))
                continue;

            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            File.Copy(file, target, overwrite: true);
        }
    }

    internal static void CreateZipFromDirectory(string sourceDirectory, string zipPath)
    {
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        ZipFile.CreateFromDirectory(sourceDirectory, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
    }

    internal static bool IsUnderRoot(string rootDirectory, string candidatePath)
    {
        var root = Path.GetFullPath(rootDirectory);
        var candidate = Path.GetFullPath(candidatePath);
        if (string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
