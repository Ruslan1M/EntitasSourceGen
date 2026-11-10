namespace EntitasSourceGen.Generator.Emit;

/// <summary><c>GameContext : Entitas.Context&lt;GameEntity&gt;</c> with ContextInfo, AERC and entity factory.</summary>
internal static class ContextEmitter
{
    public static void Emit(CodeWriter w, Scope s)
    {
        using (w.Block($"public sealed partial class {s.Context} : global::Entitas.Context<{s.EntityRef}>"))
        {
            w.Line($"public {s.Context}()");
            w.Line("    : base(");
            w.Line($"        {s.LookupRef}.TotalComponents,");
            w.Line("        0,");
            w.Line($"        new global::Entitas.ContextInfo(\"{s.Ctx.Name}\", {s.LookupRef}.componentNames, {s.LookupRef}.componentTypes),");
            w.Line("        entity =>");
            w.Raw("#if ENTITAS_FAST_AND_UNSAFE");
            w.Line("            new global::Entitas.UnsafeAERC(),");
            w.Raw("#else");
            w.Line("            new global::Entitas.SafeAERC(entity),");
            w.Raw("#endif");
            w.Line($"        () => new {s.EntityRef}())");
            w.Line("{");
            w.Line("}");
        }
    }
}
