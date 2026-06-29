using System.Collections.Immutable;
using System.Reflection;
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
        return BuildAndEmit(
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
public sealed class SettingsVmAttribute : Attribute
{
    public SettingsVmAttribute(Type settingsType) { }
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
        return BuildAndEmit(
            [
                new SettingsModelGenerator().AsSourceGenerator(),
                new ObservableSettingsGenerator().AsSourceGenerator()
            ],
            ObservableStubsSource,
            userSources.Prepend(ObservableSettingsStubsSource).ToArray());
    }

    protected const string SettingsDraftVmStubsSource = @"
using System;
using System.ComponentModel;

namespace ComposableSettings;

[AttributeUsage(AttributeTargets.Class)]
public sealed class SettingsDraftVmAttribute : Attribute
{
    public SettingsDraftVmAttribute(Type documentType) { }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class SettingsDraftRootAttribute : Attribute
{
    public SettingsDraftRootAttribute(string path) { }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingsProxyAttribute : Attribute
{
    public SettingsProxyAttribute() { }
    public SettingsProxyAttribute(string memberPath) { }
}

public abstract class ObservableObjectStub : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
";

    protected (
        ImmutableArray<Diagnostic> Diagnostics,
        Dictionary<string, string> GeneratedSources) CompileAndRunSettingsDraftVmGenerator(params string[] userSources)
    {
        return BuildAndEmit(
            [new SettingsDraftVmGenerator().AsSourceGenerator()],
            ObservableStubsSource,
            userSources.Prepend(SettingsDraftVmStubsSource).ToArray());
    }

    protected const string BehaviorTestInfrastructureSource = @"
using System;
using System.ComponentModel;

namespace ComposableSettings.Testing;

public sealed class TestSettingsProvider<TSettings> : ISettingsProvider<TSettings>
    where TSettings : class, INotifyPropertyChanged, new()
{
    private TSettings _current = new();

    public TSettings Current => _current;

    public event EventHandler<TSettings>? Replaced;

    public void Reset() => ReplaceWithNew();

    public void Reload() => ReplaceWithNew();

    public void ReplaceWithNew()
    {
        _current = new TSettings();
        Replaced?.Invoke(this, _current);
    }
}
";

    protected Assembly CompileEmitObservableSettingsAssembly(params string[] userSources)
    {
        var (diagnostics, _, compilation) = BuildObservableSettingsCompilation(
            userSources.Prepend(BehaviorTestInfrastructureSource).ToArray());

        var emitDiagnostics = EmitCompilation(compilation, out var assemblyBytes);
        var allDiagnostics = FilterDiagnostics(diagnostics.AddRange(emitDiagnostics));

        if (!allDiagnostics.IsEmpty)
        {
            foreach (var diagnostic in allDiagnostics)
                output.WriteLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");
            throw new InvalidOperationException(
                $"Compilation failed with {allDiagnostics.Length} diagnostic(s): "
                + string.Join("; ", allDiagnostics.Select(d => $"{d.Id}: {d.GetMessage()}")));
        }

        return Assembly.Load(assemblyBytes);
    }

    protected object CompileEmitAndInvokeObservableSettings(
        string fullyQualifiedTypeName,
        string methodName = "Run",
        params string[] userSources)
    {
        var assembly = CompileEmitObservableSettingsAssembly(userSources);
        var type = assembly.GetType(fullyQualifiedTypeName, throwOnError: true)
                   ?? throw new InvalidOperationException($"Type not found: {fullyQualifiedTypeName}");
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Static method not found: {methodName}");
        return method.Invoke(null, null)!;
    }

    private (
        ImmutableArray<Diagnostic> Diagnostics,
        Dictionary<string, string> GeneratedSources,
        Compilation Compilation) BuildObservableSettingsCompilation(params string[] userSources)
    {
        return BuildCompilation(
            [
                new SettingsModelGenerator().AsSourceGenerator(),
                new ObservableSettingsGenerator().AsSourceGenerator()
            ],
            ObservableStubsSource,
            userSources.Prepend(ObservableSettingsStubsSource).ToArray());
    }

    private static ImmutableArray<Diagnostic> EmitCompilation(Compilation compilation, out byte[] assemblyBytes)
    {
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        assemblyBytes = stream.ToArray();
        return emitResult.Diagnostics;
    }

    private static ImmutableArray<Diagnostic> FilterDiagnostics(ImmutableArray<Diagnostic> diagnostics)
        => diagnostics
            .Where(diagnostic =>
                diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning
                && diagnostic.Id != "CS0436"
                && diagnostic.Id != "CS8632")
            .ToImmutableArray();

    private (
        ImmutableArray<Diagnostic> Diagnostics,
        Dictionary<string, string> GeneratedSources) BuildAndEmit(
            ISourceGenerator[] generators,
            string? stubSource,
            params string[] userSources)
    {
        var (diagnostics, generatedSources, compilation) = BuildCompilation(generators, stubSource, userSources);
        var emitDiagnostics = EmitCompilation(compilation, out _);
        var allDiagnostics = FilterDiagnostics(diagnostics.AddRange(emitDiagnostics));

        foreach (var diagnostic in allDiagnostics)
            output.WriteLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");

        return (allDiagnostics, generatedSources);
    }

    private (
        ImmutableArray<Diagnostic> Diagnostics,
        Dictionary<string, string> GeneratedSources,
        Compilation Compilation) BuildCompilation(
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

        return (generatorDiagnostics, generatedSources, outCompilation);
    }
}
