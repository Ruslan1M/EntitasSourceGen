using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EntitasSourceGen.Generator.Model;

namespace EntitasSourceGen.Generator.Emit;

/// <summary>One [PrimaryEntityIndex] / [EntityIndex] member bound to a context.</summary>
internal readonly struct IndexInfo
{
    public IndexInfo(ComponentModel component, FieldModel field)
    {
        Component = component;
        Field = field;
    }

    public ComponentModel Component { get; }
    public FieldModel Field { get; }

    public bool IsPrimary => Field.Index == IndexKind.Primary;

    public string Name => Analysis.IndexName(Component, Field);

    public static IEnumerable<IndexInfo> Of(ContextModel ctx)
        => ctx.Components.Where(cc => cc.Component.GenerateMethods).SelectMany(cc => cc.Component.Fields
            .Where(f => f.Index != IndexKind.None)
            .Select(f => new IndexInfo(cc.Component, f)));
}

/// <summary>
/// Entity indices: <c>GetEntityWithX</c> / <c>GetEntitiesWithX</c> on the context, and explicit registration
/// in <c>Contexts.InitializeEntityIndices</c> (no reflection, no PostConstructor).
/// </summary>
internal static class IndexEmitter
{
    public static bool HasAny(ContextModel ctx) => IndexInfo.Of(ctx).Any();

    /// <summary><c>Game.EntityIndices.g.cs</c>: lookup methods on <c>GameContext</c>.</summary>
    public static void EmitContext(CodeWriter w, Scope s, string q)
    {
        using (w.Block($"public sealed partial class {s.Context}"))
        {
            var first = true;
            foreach (var index in IndexInfo.Of(s.Ctx))
            {
                if (!first) w.Line();
                first = false;

                var key = index.Field.Type;
                var param = Names.LowerFirst(index.Field.Name);
                var nameRef = $"{q}Contexts.{Names.Escape(index.Name)}";
                if (index.IsPrimary)
                {
                    using (w.Block($"public {s.EntityRef} GetEntityWith{index.Name}({key} {param})"))
                        w.Line($"return ((global::Entitas.PrimaryEntityIndex<{s.EntityRef}, {key}>)GetEntityIndex({nameRef})).GetEntity({param});");
                }
                else
                {
                    using (w.Block($"public global::System.Collections.Generic.HashSet<{s.EntityRef}> GetEntitiesWith{index.Name}({key} {param})"))
                        w.Line($"return ((global::Entitas.EntityIndex<{s.EntityRef}, {key}>)GetEntityIndex({nameRef})).GetEntities({param});");
                }
            }
        }
    }

    /// <summary><c>Contexts.EntityIndices.g.cs</c>: index name constants + registration for every context.</summary>
    public static void EmitContexts(CodeWriter w, ImmutableArray<ContextModel> contexts, string q)
    {
        using (w.Block("public partial class Contexts"))
        {
            var names = contexts.SelectMany(IndexInfo.Of).Select(i => i.Name).Distinct().OrderBy(n => n, System.StringComparer.Ordinal);
            foreach (var name in names)
                w.Line($"public const string {Names.Escape(name)} = \"{name}\";");
            w.Line();

            using (w.Block("partial void InitializeEntityIndices()"))
            {
                foreach (var ctx in contexts)
                {
                    var s = new Scope(ctx, q);
                    var ctxField = Names.LowerFirst(ctx.Name);
                    foreach (var index in IndexInfo.Of(ctx))
                    {
                        var c = index.Component;
                        var kind = index.IsPrimary ? "PrimaryEntityIndex" : "EntityIndex";
                        var member = Names.Escape(index.Field.Name);
                        w.Line($"{ctxField}.AddEntityIndex(new global::Entitas.{kind}<{s.EntityRef}, {index.Field.Type}>(");
                        w.Line($"    {Names.Escape(index.Name)},");
                        w.Line($"    {ctxField}.GetGroup({s.MatcherOf(c)}),");
                        // Entitas passes c == null when indexing existing entities on activation.
                        w.Line($"    (e, c) => (({c.FullTypeName})(c ?? e.GetComponent({s.IndexRef(c)}))).{member}));");
                    }
                }
            }
        }
    }
}
