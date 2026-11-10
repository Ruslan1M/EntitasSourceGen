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
        => CreateCompilation(CSharp9Options, Enumerable.Empty<MetadataReference>(), sources);

    private static CSharpCompilation CreateCompilation(CSharpParseOptions options, IEnumerable<MetadataReference> extra, string[] sources)
    {
        var trees = sources.Select((s, i) => CSharpSyntaxTree.ParseText(s, options, path: $"Source{i}.cs"))
            .Concat(AttributeTrees.Value.Select(t => t.WithRootAndOptions(t.GetRoot(), options)));
        return CSharpCompilation.Create(
            "Consumer",
            trees,
            References.Value.Concat(extra),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable));
    }

    public static GeneratorDriver CreateDriver(bool trackSteps = false, CSharpParseOptions? options = null)
        => CSharpGeneratorDriver.Create(
            new[] { new EntitasGenerator().AsSourceGenerator() },
            parseOptions: options ?? CSharp9Options,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: trackSteps));

    public static Result Run(params string[] sources)
    {
        var compilation = CreateCompilation(sources);
        var driver = CreateDriver().RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
        return new Result(driver.GetRunResult(), output, generatorDiagnostics);
    }

    /// <summary>Like <see cref="Run(string[])"/> with preprocessor symbols and extra references (e.g. UnityEngine).</summary>
    public static Result RunWith(string[] defines, IEnumerable<MetadataReference> extra, params string[] sources)
    {
        var options = CSharp9Options.WithPreprocessorSymbols(defines);
        var compilation = CreateCompilation(options, extra, sources);
        var driver = CreateDriver(options: options).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
        return new Result(driver.GetRunResult(), output, generatorDiagnostics);
    }

    /// <summary>UnityEngine assemblies of a local Unity install (env UNITY_MANAGED or the default path); empty if missing.</summary>
    public static IReadOnlyList<MetadataReference> UnityReferences()
    {
        var dir = Environment.GetEnvironmentVariable("UNITY_MANAGED")
                  ?? @"D:\Unity\Editors\6000.3.27f1\Editor\Data\Managed\UnityEngine";
        return Directory.Exists(dir)
            ? Directory.GetFiles(dir, "UnityEngine*.dll").Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList()
            : new List<MetadataReference>();
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
