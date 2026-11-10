using System.Linq;
using EntitasSourceGen.Generator.Model;

namespace EntitasSourceGen.Generator.Emit;

/// <summary><c>{Ctx}.Cleanup.g.cs</c>: one ICleanupSystem per [Cleanup] component + <c>{Ctx}CleanupSystems : Feature</c>.</summary>
internal static class CleanupEmitter
{
    public static string SystemName(ComponentModel c, string ctx)
        => (c.Cleanup == CleanupKind.DestroyEntity ? "Destroy" : "Remove") + c.Name + ctx + "System";

    public static void Emit(CodeWriter w, Scope s)
    {
        var entities = $"global::System.Collections.Generic.List<{s.EntityRef}>";
        var ordered = s.Ctx.Cleanups.OrderBy(c => c.Name, System.StringComparer.Ordinal).ToList();

        foreach (var c in ordered)
        {
            var name = SystemName(c, s.Ctx.Name);
            using (w.Block($"public sealed class {name} : global::Entitas.ICleanupSystem"))
            {
                w.Line($"readonly global::Entitas.IGroup<{s.EntityRef}> _group;");
                w.Line($"readonly {entities} _buffer = new {entities}();");
                w.Line();
                w.Line($"public {name}({s.Q}Contexts contexts) {{ _group = contexts.{Names.LowerFirst(s.Ctx.Name)}.GetGroup({s.MatcherOf(c)}); }}");
                w.Line();
                using (w.Block("public void Cleanup()"))
                {
                    var action = c.Cleanup == CleanupKind.DestroyEntity
                        ? "e.Destroy();"
                        : c.IsFlag ? $"e.{c.FlagId} = false;" : $"e.Remove{c.Name}();";
                    w.Line($"foreach (var e in _group.GetEntities(_buffer)) {action}");
                }
            }
            w.Line();
        }

        using (w.Block($"public sealed class {s.Ctx.Name}CleanupSystems : {s.Q}Feature"))
        {
            using (w.Block($"public {s.Ctx.Name}CleanupSystems({s.Q}Contexts contexts)"))
            {
                foreach (var c in ordered)
                    w.Line($"Add(new {s.Q}{SystemName(c, s.Ctx.Name)}(contexts));");
            }
        }
    }
}
