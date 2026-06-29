using ComposableSettings.Generator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ComposableSettings.Generator.Tests;

public sealed class DocumentMemberPathResolverTests
{
    [Fact]
    public void Resolves_nested_path_on_poco_document()
    {
        const string source = """
            namespace TestDocs;

            public class PaddingSection { public double Horizontal { get; set; } }
            public class TerminalSection { public PaddingSection Padding { get; set; } = new(); }
            public class AppDocument { public TerminalSection Terminal { get; set; } = new(); }
            """;

        var documentType = GetNamedType(source, "TestDocs.AppDocument");
        Assert.True(DocumentMemberPathResolver.TryResolvePath(
            documentType,
            "Terminal.Padding.Horizontal",
            out var leafType,
            out var suffix));

        Assert.Equal("double", leafType.ToDisplayString());
        Assert.Equal("Terminal.Padding.Horizontal", suffix);
    }

    private static INamedTypeSymbol GetNamedType(string source, string fullName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "DocumentMemberPathResolverTests",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .First(d => d.Identifier.Text == fullName.Split('.')[^1]);

        return semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol
               ?? throw new InvalidOperationException("Type not found.");
    }
}
