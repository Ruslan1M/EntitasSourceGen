using EntitasSourceGen.Generator.Model;

namespace EntitasSourceGen.Generator.Emit;

/// <summary><c>GameEntity</c>: x / hasX / AddX / ReplaceX / RemoveX, isX for flags.</summary>
internal static class EntityEmitter
{
    public static void Emit(CodeWriter w, Scope s)
    {
        using (w.Block($"public sealed partial class {s.Entity} : global::Entitas.Entity"))
        {
            var first = true;
            foreach (var cc in s.Ctx.Components)
            {
                if (!cc.Component.GenerateMethods) continue;
                if (!first) w.Line();
                first = false;
                if (cc.Component.IsFlag) EmitFlag(w, s, cc.Component);
                else EmitData(w, s, cc.Component);
            }
        }
    }

    private static void EmitData(CodeWriter w, Scope s, ComponentModel c)
    {
        var index = s.IndexRef(c);
        var type = c.FullTypeName;
        var lower = Names.LowerFirst(c.Name);
        var parameters = Emitter.ParamNames(c);

        w.Line($"public {type} {lower} {{ get {{ return ({type})GetComponent({index}); }} }}");
        w.Line($"public bool has{c.Name} {{ get {{ return HasComponent({index}); }} }}");
        w.Line();

        foreach (var (verb, call) in new[] { ("Add", "AddComponent"), ("Replace", "ReplaceComponent") })
        {
            using (w.Block($"public void {verb}{c.Name}({Emitter.ParamList(c)})"))
            {
                w.Line($"var index = {index};");
                w.Line($"var component = CreateComponent<{type}>(index);");
                for (var i = 0; i < c.Fields.Length; i++)
                    w.Line($"component.{Names.Escape(c.Fields[i].Name)} = {parameters[i]};");
                w.Line($"{call}(index, component);");
            }
            w.Line();
        }

        w.Line($"public void Remove{c.Name}() {{ RemoveComponent({index}); }}");
    }

    private static void EmitFlag(CodeWriter w, Scope s, ComponentModel c)
    {
        var index = s.IndexRef(c);
        var type = c.FullTypeName;
        var instance = "_" + Names.LowerFirst(c.Name).TrimStart('@') + "Component";

        // One shared instance: flags carry no data, so adding them never allocates.
        w.Line($"static readonly {type} {instance} = new {type}();");
        w.Line();
        using (w.Block($"public bool {c.FlagId}"))
        {
            w.Line($"get {{ return HasComponent({index}); }}");
            using (w.Block("set"))
            {
                using (w.Block($"if (value != {c.FlagId})"))
                {
                    w.Line($"var index = {index};");
                    using (w.Block("if (value)"))
                    {
                        w.Line("var componentPool = GetComponentPool(index);");
                        w.Line($"var component = componentPool.Count > 0 ? componentPool.Pop() : {instance};");
                        w.Line("AddComponent(index, component);");
                    }
                    using (w.Block("else"))
                    {
                        w.Line("RemoveComponent(index);");
                    }
                }
            }
        }
    }
}
