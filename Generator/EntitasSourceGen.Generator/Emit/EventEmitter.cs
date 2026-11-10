using System.Linq;
using EntitasSourceGen.Generator.Model;

namespace EntitasSourceGen.Generator.Emit;

/// <summary>
/// <c>{Ctx}.Events.g.cs</c>: listener interfaces, listener components (+ entity helpers), event systems
/// and <c>{Ctx}EventSystems : Feature</c>. Listener components are already part of the context model,
/// so their regular entity API (AddXListener(List) etc.) comes from <see cref="EntityEmitter"/>.
/// </summary>
internal static class EventEmitter
{
    public static void Emit(CodeWriter w, Scope s)
    {
        var events = s.Ctx.Events.OrderBy(e => e.BaseName, System.StringComparer.Ordinal).ToList();

        foreach (var e in events)
        {
            EmitInterface(w, s, e);
            w.Line();
            EmitListenerComponent(w, e);
            w.Line();
        }

        using (w.Block($"public sealed partial class {s.Entity}"))
        {
            var first = true;
            foreach (var e in events)
            {
                if (!first) w.Line();
                first = false;
                EmitListenerHelpers(w, e);
            }
        }

        foreach (var e in events)
        {
            w.Line();
            if (e.IsAny) EmitAnySystem(w, s, e);
            else EmitSelfSystem(w, s, e);
        }

        w.Line();
        using (w.Block($"public sealed class {s.Ctx.Name}EventSystems : {s.Q}Feature"))
        {
            using (w.Block($"public {s.Ctx.Name}EventSystems({s.Q}Contexts contexts)"))
            {
                foreach (var e in s.Ctx.Events.OrderBy(e => e.Priority).ThenBy(e => e.BaseName, System.StringComparer.Ordinal))
                    w.Line($"Add(new {s.Q}{e.SystemName}(contexts));");
            }
        }
    }

    private static string ListenerParams(Scope s, EventInfo e)
    {
        var parts = new System.Collections.Generic.List<string> { $"{s.EntityRef} entity" };
        if (HasPayload(e))
        {
            var names = PayloadNames(e.Source);
            for (var i = 0; i < e.Source.Fields.Length; i++)
                parts.Add($"{e.Source.Fields[i].Type} {names[i]}");
        }
        return string.Join(", ", parts);
    }

    /// <summary>Listener payload parameter names, unique and never <c>entity</c>.</summary>
    private static System.Collections.Generic.List<string> PayloadNames(ComponentModel c)
    {
        var used = new System.Collections.Generic.HashSet<string> { "entity" };
        var result = new System.Collections.Generic.List<string>();
        foreach (var param in Emitter.ParamNames(c))
        {
            var name = PayloadName(param);
            var unique = name;
            for (var i = 2; !used.Add(unique.TrimStart('@')); i++) unique = name + i;
            result.Add(unique);
        }
        return result;
    }

    /// <summary>Payload parameter: <c>newValue</c> → <c>value</c> (Jenny naming).</summary>
    private static string PayloadName(string param)
    {
        var name = Names.LowerFirst(param.Substring(3));
        return name == "entity" ? "entityValue" : name;
    }

    private static bool HasPayload(EventInfo e) => !e.IsRemoved && !e.Source.IsFlag;

    private static string PayloadArgs(EventInfo e)
        => string.Concat(e.Source.Fields.Select(f => ", component." + Names.Escape(f.Name)));

    private static void EmitInterface(CodeWriter w, Scope s, EventInfo e)
    {
        using (w.Block($"public interface {e.InterfaceName}"))
            w.Line($"void {e.MethodName}({ListenerParams(s, e)});");
    }

    private static void EmitListenerComponent(CodeWriter w, EventInfo e)
    {
        using (w.Block($"public sealed class {e.Listener.Name}Component : global::Entitas.IComponent"))
            w.Line($"public {e.Listener.Fields[0].Type} value;");
    }

    private static void EmitListenerHelpers(CodeWriter w, EventInfo e)
    {
        var name = e.Listener.Name;
        var lower = Names.LowerFirst(name);
        var listType = e.Listener.Fields[0].Type;

        using (w.Block($"public void Add{name}({e.InterfaceRef} value)"))
        {
            w.Line($"var listeners = has{name} ? {lower}.value : new {listType}();");
            w.Line("listeners.Add(value);");
            w.Line($"Replace{name}(listeners);");
        }
        w.Line();
        using (w.Block($"public void Remove{name}({e.InterfaceRef} value, bool removeComponentWhenEmpty = true)"))
        {
            w.Line($"var listeners = {lower}.value;");
            w.Line("listeners.Remove(value);");
            w.Line($"if (removeComponentWhenEmpty && listeners.Count == 0) Remove{name}();");
            w.Line($"else Replace{name}(listeners);");
        }
    }

    private static string Has(EventInfo e, string entity)
        => e.Source.IsFlag ? $"{entity}.{e.Source.FlagId}" : $"{entity}.has{e.Source.Name}";

    private static void EmitTrigger(CodeWriter w, Scope s, EventInfo e)
    {
        var trigger = e.IsRemoved ? "Removed" : "Added";
        w.Line($"protected override global::Entitas.ICollector<{s.EntityRef}> GetTrigger(global::Entitas.IContext<{s.EntityRef}> context)");
        w.Line("{");
        w.Line($"    return global::Entitas.CollectorContextExtension.CreateCollector(context, global::Entitas.TriggerOnEventMatcherExtension.{trigger}({s.MatcherOf(e.Source)}));");
        w.Line("}");
    }

    private static void EmitSelfSystem(CodeWriter w, Scope s, EventInfo e)
    {
        var listenerLower = Names.LowerFirst(e.Listener.Name);
        var list = $"global::System.Collections.Generic.List<{e.InterfaceRef}>";

        using (w.Block($"public sealed class {e.SystemName} : global::Entitas.ReactiveSystem<{s.EntityRef}>"))
        {
            w.Line($"readonly {list} _listenerBuffer = new {list}();");
            w.Line();
            w.Line($"public {e.SystemName}({s.Q}Contexts contexts) : base(contexts.{Names.LowerFirst(s.Ctx.Name)}) {{ }}");
            w.Line();
            EmitTrigger(w, s, e);
            w.Line();
            var present = e.IsRemoved ? "!" + Has(e, "entity") : Has(e, "entity");
            w.Line($"protected override bool Filter({s.EntityRef} entity) {{ return {present} && entity.has{e.Listener.Name}; }}");
            w.Line();
            using (w.Block($"protected override void Execute(global::System.Collections.Generic.List<{s.EntityRef}> entities)"))
            {
                using (w.Block("foreach (var e in entities)"))
                {
                    if (HasPayload(e)) w.Line($"var component = e.{Names.LowerFirst(e.Source.Name)};");
                    w.Line("_listenerBuffer.Clear();");
                    w.Line($"_listenerBuffer.AddRange(e.{listenerLower}.value);");
                    w.Line($"foreach (var listener in _listenerBuffer) listener.{e.MethodName}(e{(HasPayload(e) ? PayloadArgs(e) : "")});");
                }
            }
        }
    }

    private static void EmitAnySystem(CodeWriter w, Scope s, EventInfo e)
    {
        var listenerLower = Names.LowerFirst(e.Listener.Name);
        var list = $"global::System.Collections.Generic.List<{e.InterfaceRef}>";
        var entities = $"global::System.Collections.Generic.List<{s.EntityRef}>";

        using (w.Block($"public sealed class {e.SystemName} : global::Entitas.ReactiveSystem<{s.EntityRef}>"))
        {
            w.Line($"readonly global::Entitas.IGroup<{s.EntityRef}> _listeners;");
            w.Line($"readonly {entities} _entityBuffer = new {entities}();");
            w.Line($"readonly {list} _listenerBuffer = new {list}();");
            w.Line();
            using (w.Block($"public {e.SystemName}({s.Q}Contexts contexts) : base(contexts.{Names.LowerFirst(s.Ctx.Name)})"))
                w.Line($"_listeners = contexts.{Names.LowerFirst(s.Ctx.Name)}.GetGroup({s.MatcherRef}.{Names.Escape(e.Listener.Name)});");
            w.Line();
            EmitTrigger(w, s, e);
            w.Line();
            var present = e.IsRemoved ? "!" + Has(e, "entity") : Has(e, "entity");
            w.Line($"protected override bool Filter({s.EntityRef} entity) {{ return {present}; }}");
            w.Line();
            using (w.Block($"protected override void Execute({entities} entities)"))
            {
                using (w.Block("foreach (var e in entities)"))
                {
                    if (HasPayload(e)) w.Line($"var component = e.{Names.LowerFirst(e.Source.Name)};");
                    using (w.Block("foreach (var listenerEntity in _listeners.GetEntities(_entityBuffer))"))
                    {
                        w.Line("_listenerBuffer.Clear();");
                        w.Line($"_listenerBuffer.AddRange(listenerEntity.{listenerLower}.value);");
                        w.Line($"foreach (var listener in _listenerBuffer) listener.{e.MethodName}(e{(HasPayload(e) ? PayloadArgs(e) : "")});");
                    }
                }
            }
        }
    }
}
