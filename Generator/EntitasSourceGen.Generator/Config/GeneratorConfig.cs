using System.Linq;
using Microsoft.CodeAnalysis;

namespace EntitasSourceGen.Generator.Config;

/// <summary>
/// Settings read from <c>[assembly: EntitasSourceGen(...)]</c>. Null config = generator is off.
/// <see cref="VisualDebugging"/> / <see cref="Profiler"/>: the assembly references
/// <c>Entitas.VisualDebugging.Unity</c> / <c>UnityEngine.Profiling</c>, so Feature can use them.
/// </summary>
internal sealed record GeneratorConfig(
    string? Namespace,
    bool Singleton,
    string? InvalidNamespace = null,
    bool VisualDebugging = false,
    bool Profiler = false)
{
    public const string MarkerAttributeName = "EntitasSourceGen.EntitasSourceGenAttribute";
    public const string DebugSystemsName = "Entitas.VisualDebugging.Unity.DebugSystems";
    public const string ProfilerName = "UnityEngine.Profiling.Profiler";

    public static GeneratorConfig? FromCompilation(Compilation compilation)
    {
        var markerType = compilation.GetTypeByMetadataName(MarkerAttributeName);
        if (markerType is null) return null;

        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, markerType)) continue;

            string? ns = null;
            var singleton = false;
            foreach (var arg in attribute.NamedArguments)
            {
                switch (arg.Key)
                {
                    case "Namespace":
                        ns = arg.Value.Value as string;
                        break;
                    case "Singleton":
                        singleton = arg.Value.Value is true;
                        break;
                }
            }

            var vd = compilation.GetTypeByMetadataName(DebugSystemsName) is not null;
            var profiler = compilation.GetTypeByMetadataName(ProfilerName) is not null;
            if (string.IsNullOrWhiteSpace(ns)) return new GeneratorConfig(null, singleton, null, vd, profiler);
            ns = ns!.Trim();
            var valid = ns.Split('.').All(part => Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(part)
                                                  && Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(part) == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None);
            return valid ? new GeneratorConfig(ns, singleton, null, vd, profiler) : new GeneratorConfig(null, singleton, ns, vd, profiler);
        }

        return null;
    }
}
