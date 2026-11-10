using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EntitasSourceGen.Generator.Model;
using Microsoft.CodeAnalysis;

namespace EntitasSourceGen.Generator;

/// <summary>A rule violation tied to a component (by full type name) or to the assembly (null).</summary>
internal sealed record Problem(DiagnosticDescriptor Descriptor, string? FullTypeName, EquatableArray<string> Args)
{
    public bool Equals(Problem? other)
        => other is not null && Descriptor.Id == other.Descriptor.Id && FullTypeName == other.FullTypeName && Args.Equals(other.Args);

    public override int GetHashCode() => (Descriptor.Id, FullTypeName, Args).GetHashCode();
}

internal sealed record AnalysisResult(ImmutableArray<ContextModel> Contexts, ImmutableArray<Problem> Problems, bool GenerateFeature);

/// <summary>
/// Pure validation + grouping shared by the emit and the diagnostics pipelines, so both agree on
/// which components are generated. Conflicting components are left out instead of producing broken code.
/// </summary>
internal static class Analysis
{
    // Members of Entitas.Entity / Context<T> / generated helpers that a component must not shadow.
    private static readonly HashSet<string> EntityReserved = new()
    {
        "totalComponents", "creationIndex", "isEnabled", "componentPools", "contextInfo", "aerc", "retainCount", "owners",
        "AddComponent", "RemoveComponent", "ReplaceComponent", "GetComponent", "GetComponents", "GetComponentIndices",
        "HasComponent", "HasComponents", "HasAnyComponent", "RemoveAllComponents", "GetComponentPool", "CreateComponent",
        "Retain", "Release", "Destroy", "InternalDestroy", "Initialize", "Reactivate", "RemoveAllOnEntityReleasedHandlers",
        "ToString", "Equals", "GetHashCode", "GetType",
    };

    private static readonly HashSet<string> ContextReserved = new()
    {
        "count", "totalComponents", "contextInfo", "componentPools", "reusableEntitiesCount", "retainedEntitiesCount",
        "CreateEntity", "DestroyAllEntities", "HasEntity", "GetEntities", "GetGroup", "AddEntityIndex", "GetEntityIndex",
        "ResetCreationIndex", "ClearComponentPool", "ClearComponentPools", "Reset", "RemoveAllEventHandlers",
        "ToString", "Equals", "GetHashCode", "GetType",
    };

    private static readonly HashSet<string> LookupReserved = new() { "TotalComponents", "componentNames", "componentTypes" };
    private static readonly HashSet<string> MatcherReserved = new() { "AllOf", "AnyOf", "NoneOf", "Equals", "ReferenceEquals" };
    private static readonly HashSet<string> ContextsReserved = new() { "Reset", "allContexts", "sharedInstance", "Contexts", "InitializeEntityIndices" };

    /// <param name="components">Supported, deduplicated component models.</param>
    /// <param name="declaredContexts">Context attribute names declared in the assembly.</param>
    /// <param name="existingTypes">Fully qualified (global::) names of types declared in source, for generated-type clashes.</param>
    /// <param name="qualifier"><c>global::</c> or <c>global::Ns.</c> of generated types.</param>
    public static AnalysisResult Run(
        ImmutableArray<ComponentModel> components,
        ImmutableArray<string> declaredContexts,
        ImmutableArray<string> existingTypes,
        string qualifier)
    {
        var problems = ImmutableArray.CreateBuilder<Problem>();
        var skipped = new HashSet<string>();
        var existing = new HashSet<string>(existingTypes);

        foreach (var c in components)
        {
            if (c.Contexts.IsEmpty)
            {
                problems.Add(new Problem(Diagnostics.ComponentWithoutContext, c.FullTypeName, Args(c.FullTypeName)));
            }
            else if (c.IsUnique && c.GenerateMethods && c.Contexts.Length > 1)
            {
                problems.Add(new Problem(Diagnostics.UniqueInMultipleContexts, c.FullTypeName,
                    Args(c.FullTypeName, string.Join(", ", c.Contexts))));
                skipped.Add(c.FullTypeName);
            }
        }

        var contextNames = declaredContexts
            .Concat(components.SelectMany(c => c.Contexts))
            .Distinct()
            .OrderBy(n => n, System.StringComparer.Ordinal)
            .ToList();

        if (contextNames.Count == 0)
            problems.Add(new Problem(Diagnostics.NoContexts, null, EquatableArray<string>.Empty));

        // Index name constants share the Contexts class with the context properties.
        var contextsReserved = With(ContextsReserved, contextNames.Select(n => Names.LowerFirst(n).TrimStart('@')).ToArray());

        var contexts = ImmutableArray.CreateBuilder<ContextModel>();
        // Event / cleanup types share one namespace across all contexts.
        var generatedTypes = new Dictionary<string, string>();
        foreach (var contextName in contextNames)
        {
            var scope = new ContextScope(contextName, contextsReserved);

            var members = components
                .Where(c => !skipped.Contains(c.FullTypeName) && c.Contexts.AsImmutableArray().Contains(contextName))
                .OrderBy(c => c.Name, System.StringComparer.Ordinal)
                .ThenBy(c => c.FullTypeName, System.StringComparer.Ordinal)
                .ToList();

            var kept = new List<ComponentModel>();
            foreach (var c in members)
            {
                if (scope.ByName.TryGetValue(c.Name, out var first))
                {
                    problems.Add(new Problem(Diagnostics.DuplicateComponentName, c.FullTypeName,
                        Args(first.FullTypeName, c.FullTypeName, c.Name, contextName)));
                    continue;
                }

                if (scope.TryClaim(c, out var conflict))
                    kept.Add(c);
                else
                    problems.Add(Conflict(conflict, c));
            }

            // Events: synthetic listener components join the context (the generator never sees its own output).
            var events = new List<EventInfo>();
            var systemsType = $"{qualifier}{contextName}EventSystems";
            foreach (var c in kept.ToList())
            {
                foreach (var e in c.Events)
                {
                    var prefix = c.Contexts.Length > 1 ? contextName : string.Empty;
                    var baseName = prefix + (e.IsAny ? "Any" : string.Empty) + c.Name + (e.IsRemoved ? "Removed" : string.Empty);
                    var listenerName = baseName + "Listener";
                    var interfaceRef = $"{qualifier}I{listenerName}";
                    var listener = new ComponentModel(
                        $"{qualifier}{listenerName}Component",
                        listenerName,
                        new[] { new FieldModel("value", $"global::System.Collections.Generic.List<{interfaceRef}>") }.ToEquatableArray(),
                        new[] { contextName }.ToEquatableArray(),
                        IsUnique: false,
                        IsGenerated: true);

                    var types = new[] { listener.FullTypeName, interfaceRef, $"{qualifier}{baseName}EventSystem", systemsType };
                    var clash = TypeClash(types, existing, components, generatedTypes, c.FullTypeName, shared: systemsType);
                    if (clash is not null)
                    {
                        problems.Add(Conflict(clash.Value, c));
                        continue;
                    }

                    if (scope.ByName.TryGetValue(listener.Name, out var owner))
                    {
                        problems.Add(Conflict((listener.Name, $"component '{owner.FullTypeName}'"), c));
                        continue;
                    }

                    if (!scope.TryClaim(listener, out var conflict, extraEntityMembers: new[] { "Add" + listenerName, "Remove" + listenerName }))
                    {
                        problems.Add(Conflict(conflict, c));
                        continue;
                    }

                    foreach (var t in types) generatedTypes[t] = c.FullTypeName;
                    kept.Add(listener);
                    events.Add(new EventInfo(c, listener, baseName, interfaceRef, e.IsAny, e.IsRemoved, e.Priority));
                }
            }

            var ordered = kept
                .OrderBy(c => c.Name, System.StringComparer.Ordinal)
                .ThenBy(c => c.FullTypeName, System.StringComparer.Ordinal)
                .Select((c, i) => new ContextComponent(c, i))
                .ToEquatableArray();

            // Cleanup systems.
            var cleanups = new List<ComponentModel>();
            var cleanupFeature = $"{qualifier}{contextName}CleanupSystems";
            foreach (var c in kept.Where(c => c.Cleanup != CleanupKind.None))
            {
                var types = new[] { qualifier + Emit.CleanupEmitter.SystemName(c, contextName), cleanupFeature };
                var clash = TypeClash(types, existing, components, generatedTypes, c.FullTypeName, shared: cleanupFeature);
                if (clash is not null)
                {
                    problems.Add(Conflict(clash.Value, c));
                    continue;
                }

                foreach (var t in types) generatedTypes[t] = c.FullTypeName;
                cleanups.Add(c);
            }

            contexts.Add(new ContextModel(contextName, ordered, events.ToEquatableArray(), cleanups.ToEquatableArray()));
        }

        var needsFeature = contexts.Any(c => !c.Events.IsEmpty || !c.Cleanups.IsEmpty);
        var generateFeature = needsFeature && !existing.Contains(qualifier + "Feature");
        return new AnalysisResult(contexts.ToImmutable(), problems.Distinct().ToImmutableArray(), generateFeature);
    }

    /// <summary>
    /// First generated type that clashes with a source type, a component, or a type generated for another component.
    /// <paramref name="shared"/> (the per-context Feature) may be generated for many components.
    /// </summary>
    private static (string name, string with)? TypeClash(
        IEnumerable<string> types, HashSet<string> existing, ImmutableArray<ComponentModel> components,
        Dictionary<string, string> generated, string owner, string shared)
    {
        foreach (var t in types)
        {
            var display = t.Replace("global::", string.Empty);
            if (existing.Contains(t)) return (display, "a type declared in this assembly");
            if (components.Any(x => x.FullTypeName == t)) return (display, "a component");
            if (t != shared && generated.TryGetValue(t, out var other) && other != owner)
                return (display, $"the type generated for '{other.Replace("global::", string.Empty)}'");
        }
        return null;
    }

    private static Problem Conflict((string name, string with) conflict, ComponentModel c)
        => new(Diagnostics.GeneratedNameConflict, c.FullTypeName, Args(conflict.name, c.FullTypeName, conflict.with));

    /// <summary>Names already used inside one context's generated types.</summary>
    private sealed class ContextScope
    {
        private readonly string _ctx;
        private readonly HashSet<string> _entityReserved;
        private readonly HashSet<string> _contextReserved;
        private readonly HashSet<string> _staticReserved;
        private readonly HashSet<string> _contextsReserved;

        private readonly Dictionary<string, string> _entity = new();
        private readonly Dictionary<string, string> _context = new();
        private readonly Dictionary<string, string> _static = new();
        private readonly Dictionary<string, string> _indices = new();

        public readonly Dictionary<string, ComponentModel> ByName = new();

        public ContextScope(string ctx, HashSet<string> contextsReserved)
        {
            _ctx = ctx;
            _entityReserved = With(EntityReserved, ctx + "Entity");
            _contextReserved = With(ContextReserved, ctx + "Context");
            _staticReserved = With(LookupReserved, ctx + "ComponentsLookup", ctx + "Matcher");
            _staticReserved.UnionWith(MatcherReserved);
            _contextsReserved = contextsReserved;
        }

        public bool TryClaim(ComponentModel c, out (string name, string with) conflict, IEnumerable<string>? extraEntityMembers = null)
        {
            var entityMembers = EntityMembers(c).ToList();
            var conflictOrNull =
                FindConflict(entityMembers, _entity, _entityReserved, $"{_ctx}Entity")
                ?? FindConflict(ContextMembers(c), _context, _contextReserved, $"{_ctx}Context")
                ?? FindConflict(new[] { c.Name }, _static, _staticReserved, $"{_ctx}ComponentsLookup / {_ctx}Matcher")
                ?? FindConflict(IndexNames(c), _indices, _contextsReserved, "Contexts");

            if (conflictOrNull is not null)
            {
                conflict = conflictOrNull.Value;
                return false;
            }

            ByName[c.Name] = c;
            Claim(entityMembers, _entity, c.FullTypeName);
            if (extraEntityMembers is not null) Claim(extraEntityMembers, _entity, c.FullTypeName);
            Claim(ContextMembers(c), _context, c.FullTypeName);
            Claim(new[] { c.Name }, _static, c.FullTypeName);
            Claim(IndexNames(c), _indices, c.FullTypeName);
            conflict = default;
            return true;
        }
    }

    public static IEnumerable<string> EntityMembers(ComponentModel c)
        => !c.GenerateMethods
            ? new string[0]
            : c.IsFlag
            ? new[] { c.FlagName, "_" + Names.LowerFirst(c.Name).TrimStart('@') + "Component" }
            : new[] { Names.LowerFirst(c.Name).TrimStart('@'), "has" + c.Name, "Add" + c.Name, "Replace" + c.Name, "Remove" + c.Name };

    public static IEnumerable<string> UniqueMembers(ComponentModel c)
    {
        var entity = Names.LowerFirst(c.Name).TrimStart('@') + "Entity";
        return c.IsFlag
            ? new[] { entity, c.FlagName }
            : new[] { entity, Names.LowerFirst(c.Name).TrimStart('@'), "has" + c.Name, "Set" + c.Name, "Replace" + c.Name, "Remove" + c.Name };
    }

    /// <summary>Members generated on the context: unique API and entity-index lookups.</summary>
    public static IEnumerable<string> ContextMembers(ComponentModel c)
    {
        var members = c.IsUnique && c.GenerateMethods ? UniqueMembers(c).ToList() : new List<string>();
        if (!c.GenerateMethods) return members;
        foreach (var f in c.Fields)
        {
            if (f.Index == IndexKind.None) continue;
            var name = IndexName(c, f);
            members.Add(f.Index == IndexKind.Primary ? "GetEntityWith" + name : "GetEntitiesWith" + name);
        }
        return members;
    }

    /// <summary>Jenny naming: component name, or component + member when the component has several members.</summary>
    public static string IndexName(ComponentModel c, FieldModel f)
        => c.Fields.Length == 1 ? c.Name : c.Name + Names.UpperFirst(f.Name);

    public static IEnumerable<string> IndexNames(ComponentModel c)
        => c.GenerateMethods
            ? c.Fields.Where(f => f.Index != IndexKind.None).Select(f => IndexName(c, f))
            : Enumerable.Empty<string>();

    /// <summary>Checks reserved names, names used by earlier components and duplicates inside <paramref name="names"/>.</summary>
    private static (string name, string with)? FindConflict(
        IEnumerable<string> names, Dictionary<string, string> used, HashSet<string> reserved, string owner)
    {
        var local = new HashSet<string>();
        foreach (var n in names)
        {
            if (reserved.Contains(n)) return (n, $"a built-in member of {owner}");
            if (used.TryGetValue(n, out var other)) return (n, $"the member generated for '{other}' in {owner}");
            if (!local.Add(n)) return (n, $"another member of the same component in {owner}");
        }
        return null;
    }

    private static void Claim(IEnumerable<string> names, Dictionary<string, string> used, string owner)
    {
        foreach (var n in names) used[n] = owner;
    }

    private static HashSet<string> With(HashSet<string> set, params string[] extra)
    {
        var result = new HashSet<string>(set);
        result.UnionWith(extra);
        return result;
    }

    private static EquatableArray<string> Args(params string[] args) => args.ToEquatableArray();
}
