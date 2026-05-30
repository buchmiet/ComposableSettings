using System.Collections.Immutable;
using ComposableSettings.Generator.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public abstract class GeneratorBaseClass(ITestOutputHelper output)
{
    protected const string ObservableStubsSource = @"
using System;

namespace ComposableSettings;

[AttributeUsage(AttributeTargets.Class)]
public class SettingsModelAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public class SettingsConsumerAttribute : Attribute
{
    public SettingsConsumerAttribute(Type settingsType) { }
}

public interface ISettingsProvider<TSettings>
    where TSettings : class, System.ComponentModel.INotifyPropertyChanged, new()
{
    TSettings Current { get; }
    event EventHandler<TSettings>? Replaced;
    void Reset();
    void Reload();
}
";

    protected (
        ImmutableArray<Diagnostic> Diagnostics,
        Dictionary<string, string> GeneratedSources) CompileAndRunObservableGenerators(params string[] userSources)
    {
        return CompileAndRun(
            [
                new SettingsModelGenerator().AsSourceGenerator(),
                new SettingsConsumerGenerator().AsSourceGenerator()
            ],
            ObservableStubsSource,
            userSources);
    }

    // Stubs for the ObservableSettings flavour: the new attributes plus a minimal
    // ObservableObject base exposing a protected OnPropertyChanged(string) — the
    // relay target the generator emits into (mirrors CommunityToolkit).
    protected const string ObservableSettingsStubsSource = @"
using System;
using System.ComponentModel;

namespace ComposableSettings;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ObservableSettingsAttribute : Attribute
{
    public ObservableSettingsAttribute(Type settingsType) { }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingsProxyAttribute : Attribute { }

public abstract class ObservableObjectStub : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
";

    protected (
        ImmutableArray<Diagnostic> Diagnostics,
        Dictionary<string, string> GeneratedSources) CompileAndRunObservableSettingsGenerator(params string[] userSources)
    {
        // Pass the extra stubs as a SEPARATE source tree (not string-concatenated),
        // otherwise its leading `using` lands after the first stub's namespace (CS1529).
        return CompileAndRun(
            [
                new SettingsModelGenerator().AsSourceGenerator(),
                new ObservableSettingsGenerator().AsSourceGenerator()
            ],
            ObservableStubsSource,
            userSources.Prepend(ObservableSettingsStubsSource).ToArray());
    }

    private (
        ImmutableArray<Diagnostic> Diagnostics,
        Dictionary<string, string> GeneratedSources) CompileAndRun(
            ISourceGenerator[] generators,
            string? stubSource,
            params string[] userSources)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

        var sources = stubSource is null
            ? userSources
            : userSources.Prepend(stubSource).ToArray();

        var trees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source, parseOptions))
            .ToArray();

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToList();

        references.Add(MetadataReference.CreateFromFile(
            typeof(global::ComposableSettings.SettingsModelAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "ComposableSettingsGeneratorTestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators,
            parseOptions: parseOptions);

        var driverAfterRun = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outCompilation,
            out var generatorDiagnostics);

        var generatedSources = new Dictionary<string, string>();
        foreach (var result in driverAfterRun.GetRunResult().Results)
        foreach (var generatedSource in result.GeneratedSources)
        {
            generatedSources[generatedSource.HintName] = generatedSource.SourceText.ToString();
            output.WriteLine($"Generated: {generatedSource.HintName}");
            output.WriteLine(generatedSource.SourceText.ToString());
        }

        using var stream = new MemoryStream();
        var emitResult = outCompilation.Emit(stream);
        var diagnostics = generatorDiagnostics
            .AddRange(emitResult.Diagnostics)
            .Where(diagnostic =>
                diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning
                && diagnostic.Id != "CS0436"
                && diagnostic.Id != "CS8632")
            .ToImmutableArray();

        foreach (var diagnostic in diagnostics) output.WriteLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");

        return (diagnostics, generatedSources);
    }
}
