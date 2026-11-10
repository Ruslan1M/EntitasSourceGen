using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EntitasSourceGen.Generator.Model;

namespace EntitasSourceGen.Generator.Emit;

/// <summary>
/// Entity indices: <c>GetEntityWithX</c> / <c>GetEntitiesWithX</c> on the context, and explicit registration
/// in <c>Contexts.InitializeEntityIndices</c> (no reflection, no PostConstructor).
/// A named index with several members uses a <c>ValueTuple</c> key over one group of all its components.
/// </summary>
internal static class IndexEmitter
{
    public static bool HasAny(ContextModel ctx) => !ctx.Indices.IsEmpty;

    private static string KeyType(IndexModel index)
        => index.IsComposite
            ? "(" + string.Join(", ", index.Parts.Select(p => p.Field.Type)) + ")"
            : index.Parts[0].Field.Type;

    /// <summary>
    /// Lookup parameters: the member name for a one-member index (Jenny), otherwise the component name
    /// (plus the member name when the component brings several members).
    /// </summary>
    private static IReadOnlyList<string> ParamNames(IndexModel index)
    {
        if (!index.IsComposite) return new[] { Names.LowerFirst(index.Parts[0].Field.Name) };

        var result = new List<string>();
        var used = new HashSet<string>();
        foreach (var p in index.Parts)
        {
            var single = index.Parts.Count(x => x.Component == p.Component) == 1;
            var name = Names.LowerFirst(single ? p.Component.Name : p.Component.Name + Names.UpperFirst(p.Field.Name.TrimStart('@')));
            var unique = name;
            for (var i = 2; !used.Add(unique); i++) unique = name + i;
            result.Add(unique);
        }
        return result;
    }

    /// <summary><c>Game.EntityIndices.g.cs</c>: lookup methods on <c>GameContext</c>.</summary>
    public static void EmitContext(CodeWriter w, Scope s, string q)
    {
        using (w.Block($"public sealed partial class {s.Context}"))
        {
            var first = true;
            foreach (var index in s.Ctx.Indices)
            {
                if (!first) w.Line();
                first = false;

                var key = KeyType(index);
                var names = ParamNames(index);
                var parameters = string.Join(", ", index.Parts.Select((p, i) => $"{p.Field.Type} {names[i]}"));
                var arg = index.IsComposite ? "(" + string.Join(", ", names) + ")" : names[0];
                var nameRef = $"{q}Contexts.{Names.Escape(index.Name)}";
                if (index.IsPrimary)
                {
                    using (w.Block($"public {s.EntityRef} GetEntityWith{index.Name}({parameters})"))
                        w.Line($"return ((global::Entitas.PrimaryEntityIndex<{s.EntityRef}, {key}>)GetEntityIndex({nameRef})).GetEntity({arg});");
                }
                else
                {
                    using (w.Block($"public global::System.Collections.Generic.HashSet<{s.EntityRef}> GetEntitiesWith{index.Name}({parameters})"))
                        w.Line($"return ((global::Entitas.EntityIndex<{s.EntityRef}, {key}>)GetEntityIndex({nameRef})).GetEntities({arg});");
                }
            }
        }
    }

    /// <summary><c>Contexts.EntityIndices.g.cs</c>: index name constants + registration for every context.</summary>
    public static void EmitContexts(CodeWriter w, ImmutableArray<ContextModel> contexts, string q)
    {
        using (w.Block("public partial class Contexts"))
        {
            var names = contexts.SelectMany(c => c.Indices).Select(i => i.Name).Distinct().OrderBy(n => n, System.StringComparer.Ordinal);
            foreach (var name in names)
                w.Line($"public const string {Names.Escape(name)} = \"{name}\";");
            w.Line();

            using (w.Block("partial void InitializeEntityIndices()"))
            {
                foreach (var ctx in contexts)
                {
                    var s = new Scope(ctx, q);
                    var ctxField = Names.LowerFirst(ctx.Name);
                    foreach (var index in ctx.Indices)
                    {
                        var kind = index.IsPrimary ? "PrimaryEntityIndex" : "EntityIndex";
                        var components = index.Parts.Select(p => p.Component).Distinct().ToList();
                        var matcher = components.Count == 1
                            ? s.MatcherOf(components[0])
                            : $"{s.MatcherRef}.AllOf({string.Join(", ", components.Select(s.MatcherOf))})";

                        w.Line($"{ctxField}.AddEntityIndex(new global::Entitas.{kind}<{s.EntityRef}, {KeyType(index)}>(");
                        w.Line($"    {Names.Escape(index.Name)},");
                        w.Line($"    {ctxField}.GetGroup({matcher}),");
                        // Entitas passes c == null when indexing existing entities on activation, and only the
                        // added / removed component otherwise: the other parts are read from the entity.
                        if (!index.IsComposite)
                        {
                            var p = index.Parts[0];
                            w.Line($"    (e, c) => (({p.Component.FullTypeName})(c ?? e.GetComponent({s.IndexRef(p.Component)}))).{Names.Escape(p.Field.Name)}));");
                        }
                        else
                        {
                            w.Line("    (e, c) => (");
                            for (var i = 0; i < index.Parts.Length; i++)
                            {
                                var p = index.Parts[i];
                                var type = p.Component.FullTypeName;
                                var comma = i < index.Parts.Length - 1 ? "," : ")));";
                                w.Line($"        (c as {type} ?? ({type})e.GetComponent({s.IndexRef(p.Component)})).{Names.Escape(p.Field.Name)}{comma}");
                            }
                        }
                    }
                }
            }
        }
    }
}
