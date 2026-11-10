using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EntitasSourceGen.Generator.Config;
using EntitasSourceGen.Generator.Model;
using Microsoft.CodeAnalysis;

namespace EntitasSourceGen.Generator;

internal static class DiagnosticReporter
{
    public static void Report(
        SourceProductionContext spc,
        ImmutableArray<ComponentCandidate> candidates,
        EquatableArray<string> declaredContexts,
        EquatableArray<string> existingTypes,
        GeneratorConfig? config)
    {
        // Partial classes produce one candidate per declaration: report once, at the first one.
        var unique = candidates
            .GroupBy(c => c.Model.FullTypeName)
            .Select(g => g.OrderBy(c => c.Location.FilePath, System.StringComparer.Ordinal).ThenBy(c => c.Location.Span.Start).First())
            .OrderBy(c => c.Model.FullTypeName, System.StringComparer.Ordinal)
            .ToList();

        if (config is null)
        {
            foreach (var c in unique.Where(c => !c.Model.Contexts.IsEmpty))
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.ComponentWithoutMarker, c.Location.ToLocation(), Display(c.Model.FullTypeName)));
            return;
        }

        if (config.InvalidNamespace is not null)
            spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.IgnoredAttribute, Location.None, "assembly",
                $"Namespace = \"{config.InvalidNamespace}\"", "it is not a valid C# namespace; the global namespace is used"));

        var locations = new Dictionary<string, Location>();
        foreach (var c in unique) locations[c.Model.FullTypeName] = c.Location.ToLocation();

        foreach (var c in unique)
        {
            var location = locations[c.Model.FullTypeName];
            var name = Display(c.Model.FullTypeName);
            // Without a context the component is never generated: ECG001 (warning) is enough.
            if (c.IsUnsupported && !c.Model.Contexts.IsEmpty)
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnsupportedComponentShape, location, name));
            else if (c.IsUnsupported)
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.ComponentWithoutContext, location, name));
            foreach (var member in c.AmbiguousIndexMembers)
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.AmbiguousIndex, location, name, member));
            foreach (var member in c.IgnoredIndexMembers)
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.IgnoredIndex, location, name, member));
            foreach (var entry in c.IgnoredAttributes)
            {
                var parts = entry.Split('|');
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.IgnoredAttribute, location, name, parts[0], parts[1]));
            }
        }

        var supported = unique.Where(c => !c.IsUnsupported).Select(c => c.Model).ToImmutableArray();
        var analysis = Analysis.Run(supported, declaredContexts.AsImmutableArray(), existingTypes.AsImmutableArray(), Emit.Emitter.Qualifier(config));

        foreach (var p in analysis.Problems)
        {
            var location = p.FullTypeName is not null && locations.TryGetValue(p.FullTypeName, out var l) ? l : Location.None;
            spc.ReportDiagnostic(Diagnostic.Create(p.Descriptor, location, p.Args.Select(a => (object)Display(a)).ToArray()));
        }
    }

    private static string Display(string name) => name.Replace("global::", string.Empty);
}
