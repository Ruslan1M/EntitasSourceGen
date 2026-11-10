using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EntitasSourceGen.Generator.Tests;

/// <summary>Builds a C# 9 compilation (like Unity) against real Entitas.dll and runs the generator.</summary>
internal static class GeneratorHarness
{
    public static readonly CSharpParseOptions CSharp9Options = new(LanguageVersion.CSharp9);

    public const string Marker = "[assembly: EntitasSourceGen.EntitasSourceGen]";

    public const string ContextAttributes = @"
public sealed class GameAttribute : EntitasSourceGen.ContextAttribute { }
public sealed class InputAttribute : EntitasSourceGen.ContextAttribute { }
";

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(LoadReferences);
    private static readonly Lazy<ImmutableArray<SyntaxTree>> AttributeTrees = new(LoadAttributeTrees);

    public static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var trees = sources.Select((s, i) => CSharpSyntaxTree.ParseText(s, CSharp9Options, path: $"Source{i}.cs"))
            .Concat(AttributeTrees.Value);
        return CSharpCompilation.Create(
            "Consumer",
            trees,
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable));
    }

    public static GeneratorDriver CreateDriver(bool trackSteps = false)
        => CSharpGeneratorDriver.Create(
            new[] { new EntitasGenerator().AsSourceGenerator() },
            parseOptions: CSharp9Options,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: trackSteps));

    public static Result Run(params string[] sources)
    {
        var compilation = CreateCompilation(sources);
        var driver = CreateDriver().RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
        return new Result(driver.GetRunResult(), output, generatorDiagnostics);
    }

    internal sealed record Result(GeneratorDriverRunResult RunResult, Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics)
    {
        public ImmutableArray<GeneratedSourceResult> Sources => RunResult.Results.Single().GeneratedSources;

        public string Source(string hintName) => Sources.Single(s => s.HintName == hintName).SourceText.ToString();

        public ImmutableArray<Diagnostic> CompileErrors => Output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        public ImmutableArray<Diagnostic> Diagnostics(string id) => GeneratorDiagnostics.Where(d => d.Id == id).ToImmutableArray();
    }

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var entitasDir = Path.Combine(AppContext.BaseDirectory, "Entitas");
        return tpa
            .Concat(Directory.GetFiles(entitasDir, "*.dll"))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToImmutableArray();
    }

    private static ImmutableArray<SyntaxTree> LoadAttributeTrees()
        => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Attributes"), "*.cs")
            .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), CSharp9Options, path: p))
            .ToImmutableArray();
}
