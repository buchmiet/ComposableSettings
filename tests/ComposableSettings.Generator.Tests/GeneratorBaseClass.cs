using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ComposableSettings.Generator.SourceGenerators;
using Xunit.Abstractions;

namespace ComposableSettings.Generator.Tests;

public abstract class GeneratorBaseClass(ITestOutputHelper output)
{
    protected (
        ImmutableArray<Diagnostic> Diagnostics,
        Dictionary<string, string> GeneratedSources) CompileAndRunAllGenerators(params string[] userSources)
    {
        return CompileAndRun(
            [
                new SettingsChildrenGenerator().AsSourceGenerator(),
                new SettingsRegistrationGenerator().AsSourceGenerator(),
                new SettingsDIGenerator().AsSourceGenerator(),
                new SettingsLifecycleGenerator().AsSourceGenerator()
            ],
            AllStubsSource,
            userSources);
    }

    protected (
        ImmutableArray<Diagnostic> Diagnostics,
        Dictionary<string, string> GeneratedSources) CompileAndRunGenerator(params string[] userSources)
    {
        return CompileAndRun(
            [new SettingsChildrenGenerator().AsSourceGenerator()],
            null,
            userSources);
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

        references.Add(MetadataReference.CreateFromFile(typeof(SettingsChildAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "ComposableSettingsGeneratorTestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: generators,
            parseOptions: parseOptions);

        var driverAfterRun = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outCompilation,
            out var generatorDiagnostics);

        var generatedSources = new Dictionary<string, string>();
        foreach (var result in driverAfterRun.GetRunResult().Results)
        {
            foreach (var generatedSource in result.GeneratedSources)
            {
                generatedSources[generatedSource.HintName] = generatedSource.SourceText.ToString();
                output.WriteLine($"Generated: {generatedSource.HintName}");
                output.WriteLine(generatedSource.SourceText.ToString());
            }
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

        foreach (var diagnostic in diagnostics)
        {
            output.WriteLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        return (diagnostics, generatedSources);
    }

    protected const string BaseRuntimeSource = """
        namespace ComposableSettings;

        public sealed class SettingsNodePath { }
        public interface ISettingsNodeFactory
        {
            T CreateChild<T>(SettingsNodePath parentPath, string? instanceName = null)
                where T : class;
        }
        """;

    protected const string AllStubsSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        namespace ComposableSettings;

        [AttributeUsage(AttributeTargets.Class)]
        public sealed class SettingsRootAttribute : Attribute
        {
            public SettingsRootAttribute(string name) { }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public sealed class SettingsComponentAttribute : Attribute
        {
            public SettingsComponentAttribute(string name) { }
            public SettingsComponentAttribute(string name, Type settingsType) { }
            public bool GenerateLifecycle { get; set; }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public sealed class SettingsChildAttribute : Attribute
        {
            public SettingsChildAttribute() { }
            public SettingsChildAttribute(string name) { }
        }

        public sealed class SettingsNodePath
        {
            public static SettingsNodePath Root(string segment) => new();
            public SettingsNodePath Child(string segment) => new();
        }

        public interface ISettingsNodeFactory
        {
            T CreateChild<T>(SettingsNodePath parentPath, string? instanceName = null)
                where T : class;
        }

        public interface IComponentSettingsStore
        {
            void Register<T>(SettingsNodePath path) where T : class, new();
            void CompleteRegistration(bool resetToDefaults = false);
        }

        public interface IComponentSettingsInitializer
        {
            void Initialize(bool resetToDefaults = false);
        }

        public interface IComponentSettings<TSettings>
            where TSettings : class, new()
        {
            Task SaveAsync(TSettings value, CancellationToken cancellationToken = default);
        }
        """;
}
