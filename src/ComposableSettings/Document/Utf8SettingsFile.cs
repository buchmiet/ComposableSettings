namespace ComposableSettings.Document;

internal static class Utf8SettingsFile
{
    public static byte[] ReadAllBytes(string filePath)
        => File.ReadAllBytes(filePath);

    public static void WriteAllBytes(string filePath, ReadOnlySpan<byte> utf8Json)
        => File.WriteAllBytes(filePath, utf8Json);
}
