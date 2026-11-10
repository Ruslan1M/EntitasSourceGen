using System.Linq;
using EntitasSourceGen.Generator.Model;

namespace EntitasSourceGen.Generator.Emit;

/// <summary>Context-level API for [Unique] components.</summary>
internal static class UniqueEmitter
{
    public static bool HasAny(ContextModel ctx) => ctx.Components.Any(cc => cc.Component.IsUnique && cc.Component.GenerateMethods);

    public static void Emit(CodeWriter w, Scope s)
    {
        using (w.Block($"public sealed partial class {s.Context}"))
        {
            var first = true;
            foreach (var cc in s.Ctx.Components)
            {
                if (!cc.Component.IsUnique || !cc.Component.GenerateMethods) continue;
                if (!first) w.Line();
                first = false;
                if (cc.Component.IsFlag) EmitFlag(w, s, cc.Component);
                else EmitData(w, s, cc.Component);
            }
        }
    }

    private static string EntityProp(ComponentModel c) => Names.LowerFirst(c.Name).TrimStart('@') + "Entity";

    private static void EmitData(CodeWriter w, Scope s, ComponentModel c)
    {
        var entityProp = EntityProp(c);
        var lower = Names.LowerFirst(c.Name);
        var args = Emitter.ArgList(c);

        w.Line($"public {s.EntityRef} {entityProp} {{ get {{ return GetGroup({s.MatcherOf(c)}).GetSingleEntity(); }} }}");
        w.Line($"public {c.FullTypeName} {lower} {{ get {{ return {entityProp}.{lower}; }} }}");
        if (c.HasValueProperty)
            w.Line($"public {c.Fields[0].Type} {c.Name} {{ get {{ return {entityProp}.{c.Name}; }} }}");
        w.Line($"public bool has{c.Name} {{ get {{ return {entityProp} != null; }} }}");
        w.Line();

        using (w.Block($"public {s.EntityRef} Set{c.Name}({Emitter.ParamList(c)})"))
        {
            using (w.Block($"if (has{c.Name})"))
            {
                w.Line("throw new global::Entitas.EntitasException(");
                w.Line($"    \"Could not set {c.Name}!\\n\" + this + \" already has an entity with {c.Name}!\",");
                w.Line($"    \"You should check if the context already has a {entityProp} before setting it or use context.Replace{c.Name}().\");");
            }
            w.Line();
            w.Line("var entity = CreateEntity();");
            w.Line($"entity.Add{c.Name}({args});");
            w.Line("return entity;");
        }
        w.Line();

        using (w.Block($"public void Replace{c.Name}({Emitter.ParamList(c)})"))
        {
            w.Line($"var entity = {entityProp};");
            w.Line($"if (entity == null) entity = Set{c.Name}({args});");
            w.Line($"else entity.Replace{c.Name}({args});");
        }
        w.Line();

        w.Line($"public void Remove{c.Name}() {{ {entityProp}.Destroy(); }}");
    }

    private static void EmitFlag(CodeWriter w, Scope s, ComponentModel c)
    {
        var entityProp = EntityProp(c);

        w.Line($"public {s.EntityRef} {entityProp} {{ get {{ return GetGroup({s.MatcherOf(c)}).GetSingleEntity(); }} }}");
        w.Line();
        using (w.Block($"public bool {c.FlagId}"))
        {
            w.Line($"get {{ return {entityProp} != null; }}");
            using (w.Block("set"))
            {
                w.Line($"var entity = {entityProp};");
                using (w.Block("if (value != (entity != null))"))
                {
                    w.Line($"if (value) CreateEntity().{c.FlagId} = true;");
                    w.Line("else entity.Destroy();");
                }
            }
        }
    }
}
