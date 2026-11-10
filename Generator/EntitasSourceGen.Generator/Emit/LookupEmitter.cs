namespace EntitasSourceGen.Generator.Emit;

/// <summary><c>GameComponentsLookup</c>: indices, TotalComponents, names, types.</summary>
internal static class LookupEmitter
{
    public static void Emit(CodeWriter w, Scope s)
    {
        using (w.Block($"public static class {s.Lookup}"))
        {
            foreach (var cc in s.Ctx.Components)
                w.Line($"public const int {cc.Component.Id} = {cc.Index};");
            w.Line();
            w.Line($"public const int TotalComponents = {s.Ctx.Components.Length};");
            w.Line();

            w.Line("public static readonly string[] componentNames =");
            w.Line("{");
            foreach (var cc in s.Ctx.Components)
                w.Line($"    \"{cc.Component.Name}\",");
            w.Line("};");
            w.Line();

            w.Line("public static readonly global::System.Type[] componentTypes =");
            w.Line("{");
            foreach (var cc in s.Ctx.Components)
                w.Line($"    typeof({cc.Component.FullTypeName}),");
            w.Line("};");
        }
    }
}
