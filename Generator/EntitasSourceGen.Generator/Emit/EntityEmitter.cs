using System.Collections.Immutable;
using System.Linq;
using EntitasSourceGen.Generator.Model;

namespace EntitasSourceGen.Generator.Emit;

/// <summary>
/// <c>GameEntity</c>: x / hasX / AddX / ReplaceX / RemoveX (fluent: they return the entity), isX for flags,
/// <c>X</c> for single-<c>Value</c> components, and <c>I{X}Entity</c> for components shared by several contexts.
/// </summary>
internal static class EntityEmitter
{
    public static void Emit(CodeWriter w, Scope s)
    {
        var shared = s.Ctx.Components
            .Select(cc => cc.Component)
            .Where(c => s.Ctx.Interfaces.AsImmutableArray().Contains(c.FullTypeName))
            .ToList();

        var header = $"public sealed partial class {s.Entity} : global::Entitas.Entity";
        if (shared.Count == 0)
        {
            w.Open(header);
        }
        else
        {
            w.Line(header + ",");
            for (var i = 0; i < shared.Count; i++)
            {
                var c = shared[i];
                var list = c.IsFlag ? $"{s.Q}{c.InterfaceName}" : $"{s.Q}{c.InterfaceName}<{s.EntityRef}>, {s.Q}{c.InterfaceName}";
                var line = "    " + list + (i < shared.Count - 1 ? "," : string.Empty);
                if (i < shared.Count - 1) w.Line(line);
                else w.Open(line);
            }
        }

        var first = true;
        foreach (var cc in s.Ctx.Components)
        {
            if (!cc.Component.GenerateMethods) continue;
            if (!first) w.Line();
            first = false;
            if (cc.Component.IsFlag) EmitFlag(w, s, cc.Component);
            else EmitData(w, s, cc.Component, shared.Contains(cc.Component));
        }

        w.Close();
    }

    private static void EmitData(CodeWriter w, Scope s, ComponentModel c, bool explicitInterface)
    {
        var index = s.IndexRef(c);
        var type = c.FullTypeName;
        var lower = Names.LowerFirst(c.Name);
        var parameters = Emitter.ParamNames(c);

        w.Line($"public {type} {lower} {{ get {{ return ({type})GetComponent({index}); }} }}");
        if (c.HasValueProperty)
            w.Line($"public {c.Fields[0].Type} {c.Name} {{ get {{ return {lower}.Value; }} }}");
        w.Line($"public bool has{c.Name} {{ get {{ return HasComponent({index}); }} }}");
        w.Line();

        foreach (var (verb, call) in new[] { ("Add", "AddComponent"), ("Replace", "ReplaceComponent") })
        {
            using (w.Block($"public {s.EntityRef} {verb}{c.Name}({Emitter.ParamList(c)})"))
            {
                w.Line($"var index = {index};");
                w.Line($"var component = CreateComponent<{type}>(index);");
                for (var i = 0; i < c.Fields.Length; i++)
                    w.Line($"component.{Names.Escape(c.Fields[i].Name)} = {parameters[i]};");
                w.Line($"{call}(index, component);");
                w.Line("return this;");
            }
            w.Line();
        }

        using (w.Block($"public {s.EntityRef} Remove{c.Name}()"))
        {
            w.Line($"RemoveComponent({index});");
            w.Line("return this;");
        }

        if (!explicitInterface) return;

        // Non-generic I{X}Entity returns IEntity: the typed methods above cannot implement it implicitly.
        var iface = $"{s.Q}{c.InterfaceName}<global::Entitas.IEntity>";
        var args = Emitter.ArgList(c);
        w.Line();
        w.Line($"global::Entitas.IEntity {iface}.Add{c.Name}({Emitter.ParamList(c)}) {{ return Add{c.Name}({args}); }}");
        w.Line($"global::Entitas.IEntity {iface}.Replace{c.Name}({Emitter.ParamList(c)}) {{ return Replace{c.Name}({args}); }}");
        w.Line($"global::Entitas.IEntity {iface}.Remove{c.Name}() {{ return Remove{c.Name}(); }}");
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

    /// <summary><c>Components.Interfaces.g.cs</c>: one interface per component shared by several contexts.</summary>
    public static void EmitInterfaces(CodeWriter w, ImmutableArray<ComponentModel> components)
    {
        var first = true;
        foreach (var c in components)
        {
            if (!first) w.Line();
            first = false;

            if (c.IsFlag)
            {
                w.Line($"/// <summary>Entity of any context that has <c>{c.Name}</c>.</summary>");
                using (w.Block($"public partial interface {c.InterfaceName} : global::Entitas.IEntity"))
                    w.Line($"bool {c.FlagId} {{ get; set; }}");
                continue;
            }

            var lower = Names.LowerFirst(c.Name);
            var parameters = Emitter.ParamList(c);
            w.Line($"/// <summary>Entity of any context that has <c>{c.Name}</c>; the methods return <typeparamref name=\"TEntity\"/>.</summary>");
            using (w.Block($"public partial interface {c.InterfaceName}<TEntity> : global::Entitas.IEntity where TEntity : global::Entitas.IEntity"))
            {
                w.Line($"{c.FullTypeName} {lower} {{ get; }}");
                if (c.HasValueProperty) w.Line($"{c.Fields[0].Type} {c.Name} {{ get; }}");
                w.Line($"bool has{c.Name} {{ get; }}");
                w.Line();
                w.Line($"TEntity Add{c.Name}({parameters});");
                w.Line($"TEntity Replace{c.Name}({parameters});");
                w.Line($"TEntity Remove{c.Name}();");
            }
            w.Line();
            w.Line($"/// <summary>Non-generic <c>{c.InterfaceName}</c>: the methods return <c>Entitas.IEntity</c>.</summary>");
            w.Line($"public partial interface {c.InterfaceName} : {c.InterfaceName}<global::Entitas.IEntity> {{ }}");
        }
    }
}
