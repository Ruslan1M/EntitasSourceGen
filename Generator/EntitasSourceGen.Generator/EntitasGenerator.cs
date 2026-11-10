using System.Linq;
using EntitasSourceGen.Generator.Config;
using EntitasSourceGen.Generator.Emit;
using EntitasSourceGen.Generator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntitasSourceGen.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class EntitasGenerator : IIncrementalGenerator
{
    /// <summary>Only names shaped like generated event / cleanup / Feature / I{X}Entity types matter; others never invalidate the cache.</summary>
    private static bool CanClash(string? fullName)
    {
        if (fullName is null) return false;
        var name = fullName.Substring(fullName.LastIndexOf('.') + 1).Replace("global::", string.Empty);
        return name == "Feature" || (name.StartsWith("I") && name.EndsWith("Entity")) || name.EndsWith("Listener") || name.EndsWith("ListenerComponent")
               || name.EndsWith("EventSystem") || name.EndsWith("EventSystems") || name.EndsWith("CleanupSystems")
               || ((name.StartsWith("Remove") || name.StartsWith("Destroy")) && name.EndsWith("System"));
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var config = context.CompilationProvider
            .Select(static (compilation, _) => GeneratorConfig.FromCompilation(compilation))
            .WithTrackingName(TrackingNames.Config);

        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(ComponentParser.IsCandidate, ComponentParser.ParseComponent)
            .Where(static c => c is not null)
            .Select(static (c, _) => c!)
            .WithTrackingName(TrackingNames.Components);

        // Emit input: models only (no locations), deduplicated across partial declarations.
        var components = candidates
            .Where(static c => !c.IsUnsupported)
            .Select(static (c, _) => c.Model)
            .Collect()
            .Select(static (all, _) => all
                .Distinct()
                .OrderBy(c => c.FullTypeName, System.StringComparer.Ordinal)
                .ToEquatableArray())
            .WithTrackingName(TrackingNames.AllComponents);

        var declaredContexts = context.SyntaxProvider
            .CreateSyntaxProvider(ComponentParser.IsCandidate, ComponentParser.ParseContextAttribute)
            .Where(static n => n is not null)
            .Select(static (n, _) => n!)
            .Collect()
            .Select(static (all, _) => all.Distinct().OrderBy(n => n, System.StringComparer.Ordinal).ToEquatableArray())
            .WithTrackingName(TrackingNames.Contexts);

        // Names of types declared in source: generated event / Feature types must not clash with them.
        var existingTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax,
                static (ctx, ct) => ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is INamedTypeSymbol { ContainingType: null } t
                    ? t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    : null)
            .Where(static n => n is not null && CanClash(n))
            .Select(static (n, _) => n!)
            .Collect()
            .Select(static (all, _) => all.Distinct().OrderBy(n => n, System.StringComparer.Ordinal).ToEquatableArray())
            .WithTrackingName(TrackingNames.ExistingTypes);

        var emitInput = components.Combine(declaredContexts).Combine(existingTypes).Combine(config).WithTrackingName(TrackingNames.EmitInput);
        context.RegisterSourceOutput(emitInput, static (spc, data) =>
        {
            var (((comps, ctxNames), types), cfg) = data;
            if (cfg is null) return;
            var analysis = Analysis.Run(comps.AsImmutableArray(), ctxNames.AsImmutableArray(), types.AsImmutableArray(), Emitter.Qualifier(cfg));
            if (analysis.Contexts.IsEmpty) return;
            Emitter.EmitAll(spc, cfg, analysis);
        });

        // Diagnostics: separate pipeline, because it depends on locations.
        var diagInput = candidates.Collect().Combine(declaredContexts).Combine(existingTypes).Combine(config);
        context.RegisterSourceOutput(diagInput, static (spc, data) =>
        {
            var (((all, ctxNames), types), cfg) = data;
            DiagnosticReporter.Report(spc, all, ctxNames, types, cfg);
        });
    }
}

internal static class TrackingNames
{
    public const string Config = nameof(Config);
    public const string Components = nameof(Components);
    public const string AllComponents = nameof(AllComponents);
    public const string Contexts = nameof(Contexts);
    public const string EmitInput = nameof(EmitInput);
    public const string ExistingTypes = nameof(ExistingTypes);
}
