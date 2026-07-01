using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using ComposableSettings.Document;

namespace ComposableSettings.Benchmarks;

[MemoryDiagnoser]
public class SettingsJsonIoBenchmarks
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private SampleSettingsDocument _document = null!;
    private string _json = null!;
    private byte[] _utf8Json = null!;
    private string _filePathString = null!;
    private string _filePathUtf8 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _document = CreateSampleDocument();
        _json = JsonSerializer.Serialize(_document, SerializerOptions);
        _utf8Json = JsonSerializer.SerializeToUtf8Bytes(_document, SerializerOptions);

        var directory = Path.Combine(Path.GetTempPath(), "ComposableSettings.Benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _filePathString = Path.Combine(directory, "settings-string.json");
        _filePathUtf8 = Path.Combine(directory, "settings-utf8.json");
        File.WriteAllText(_filePathString, _json);
        File.WriteAllBytes(_filePathUtf8, _utf8Json);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_filePathString))
            File.Delete(_filePathString);
        if (File.Exists(_filePathUtf8))
            File.Delete(_filePathUtf8);

        var directory = Path.GetDirectoryName(_filePathString);
        if (directory is not null && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    [Benchmark(Baseline = true)]
    public string Serialize_ToString()
        => JsonSerializer.Serialize(_document, SerializerOptions);

    [Benchmark]
    public byte[] Serialize_ToUtf8Bytes()
        => JsonSerializer.SerializeToUtf8Bytes(_document, SerializerOptions);

    [Benchmark]
    public SampleSettingsDocument Deserialize_FromString()
        => JsonSerializer.Deserialize<SampleSettingsDocument>(_json, SerializerOptions)!;

    [Benchmark]
    public SampleSettingsDocument Deserialize_FromUtf8Bytes()
        => JsonSerializer.Deserialize<SampleSettingsDocument>(_utf8Json, SerializerOptions)!;

    [Benchmark]
    public SampleSettingsDocument FileRoundTrip_String()
    {
        File.WriteAllText(_filePathString, _json);
        var text = File.ReadAllText(_filePathString);
        return JsonSerializer.Deserialize<SampleSettingsDocument>(text, SerializerOptions)!;
    }

    [Benchmark]
    public SampleSettingsDocument FileRoundTrip_Utf8Bytes()
    {
        File.WriteAllBytes(_filePathUtf8, _utf8Json);
        var bytes = File.ReadAllBytes(_filePathUtf8);
        return JsonSerializer.Deserialize<SampleSettingsDocument>(bytes, SerializerOptions)!;
    }

    [Benchmark]
    public SampleSettingsDocument Clone_StringPath()
    {
        var json = JsonSerializer.Serialize(_document, SerializerOptions);
        return JsonSerializer.Deserialize<SampleSettingsDocument>(json, SerializerOptions)!;
    }

    [Benchmark]
    public SampleSettingsDocument Clone_Utf8Path()
    {
        var utf8 = JsonSerializer.SerializeToUtf8Bytes(_document, SerializerOptions);
        return JsonSerializer.Deserialize<SampleSettingsDocument>(utf8, SerializerOptions)!;
    }

    [Benchmark]
    public bool IsEmptyCompare_StringPath()
    {
        var empty = new SampleSettingsDocument();
        return JsonSerializer.Serialize(_document, SerializerOptions)
               == JsonSerializer.Serialize(empty, SerializerOptions);
    }

    [Benchmark]
    public bool IsEmptyCompare_Utf8Path()
    {
        var empty = new SampleSettingsDocument();
        return JsonSerializer.SerializeToUtf8Bytes(_document, SerializerOptions)
            .AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(empty, SerializerOptions));
    }

    [Benchmark]
    public string FileWrite_StringViaUtf8Transcode()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(_document, SerializerOptions);
        var text = Encoding.UTF8.GetString(bytes);
        File.WriteAllText(_filePathString, text);
        return text;
    }

    [Benchmark]
    public void Serializer_Utf8Api()
    {
        var serializer = new JsonSettingsDocumentSerializer<SampleSettingsDocument>();
        _ = serializer.Clone(_document);
    }

    private static SampleSettingsDocument CreateSampleDocument()
        => new()
        {
            ThemeId = "solarized-dark",
            PanelWidth = 420,
            EnabledPlugins = ["core", "themes", "editor", "terminal", "git"],
            Layout = new LayoutSection
            {
                Opacity = 0.85,
                SidebarWidth = 280,
                Tabs =
                [
                    new TabSection { Id = "general", Label = "General", Order = 0 },
                    new TabSection { Id = "appearance", Label = "Appearance", Order = 1 },
                    new TabSection { Id = "keybindings", Label = "Keybindings", Order = 2 },
                ],
            },
            Editor = new EditorSection
            {
                FontFamily = "Cascadia Code",
                FontSize = 14,
                ShowLineNumbers = true,
                WordWrap = false,
            },
        };

    public sealed class SampleSettingsDocument
    {
        public string ThemeId { get; set; } = "default";
        public int PanelWidth { get; set; }
        public List<string> EnabledPlugins { get; set; } = [];
        public LayoutSection Layout { get; set; } = new();
        public EditorSection Editor { get; set; } = new();
    }

    public sealed class LayoutSection
    {
        public double Opacity { get; set; } = 1.0;
        public int SidebarWidth { get; set; } = 240;
        public List<TabSection> Tabs { get; set; } = [];
    }

    public sealed class TabSection
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public int Order { get; set; }
    }

    public sealed class EditorSection
    {
        public string FontFamily { get; set; } = "Consolas";
        public int FontSize { get; set; } = 12;
        public bool ShowLineNumbers { get; set; } = true;
        public bool WordWrap { get; set; }
    }
}
