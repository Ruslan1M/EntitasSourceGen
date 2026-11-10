using System.Linq;
using Microsoft.CodeAnalysis;

namespace EntitasSourceGen.Generator.Config;

/// <summary>Settings read from <c>[assembly: EntitasSourceGen(...)]</c>. Null config = generator is off.</summary>
internal sealed record GeneratorConfig(string? Namespace, bool Singleton, string? InvalidNamespace = null)
{
    public const string MarkerAttributeName = "EntitasSourceGen.EntitasSourceGenAttribute";

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

            if (string.IsNullOrWhiteSpace(ns)) return new GeneratorConfig(null, singleton);
            ns = ns!.Trim();
            var valid = ns.Split('.').All(part => Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(part)
                                                  && Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(part) == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None);
            return valid ? new GeneratorConfig(ns, singleton) : new GeneratorConfig(null, singleton, ns);
        }

        return null;
    }
}
