using System.Collections.Immutable;
using System.Linq;
using EntitasSourceGen.Generator.Config;
using EntitasSourceGen.Generator.Model;

namespace EntitasSourceGen.Generator.Emit;

/// <summary><c>Contexts : Entitas.IContexts</c>. No singleton unless the marker enables it.</summary>
internal static class ContextsEmitter
{
    public static void Emit(CodeWriter w, GeneratorConfig config, ImmutableArray<ContextModel> contexts, string q)
    {
        var self = q + "Contexts";
        using (w.Block("public partial class Contexts : global::Entitas.IContexts"))
        {
            if (config.Singleton)
            {
                w.Line($"static {self} _sharedInstance;");
                w.Line();
                using (w.Block($"public static {self} sharedInstance"))
                {
                    w.Line($"get {{ if (_sharedInstance == null) _sharedInstance = new {self}(); return _sharedInstance; }}");
                    w.Line("set { _sharedInstance = value; }");
                }
                w.Line();
            }

            foreach (var ctx in contexts)
                w.Line($"public {q}{ctx.Name}Context {Names.LowerFirst(ctx.Name)} {{ get; set; }}");
            w.Line();

            var all = string.Join(", ", contexts.Select(c => Names.LowerFirst(c.Name)));
            w.Line($"public global::Entitas.IContext[] allContexts {{ get {{ return new global::Entitas.IContext[] {{ {all} }}; }} }}");
            w.Line();

            using (w.Block("public Contexts()"))
            {
                foreach (var ctx in contexts)
                    w.Line($"{Names.LowerFirst(ctx.Name)} = new {q}{ctx.Name}Context();");
                w.Line();
                w.Line("InitializeEntityIndices();");
            }
            w.Line();

            using (w.Block("public void Reset()"))
            {
                w.Line("var contexts = allContexts;");
                w.Line("for (int i = 0; i < contexts.Length; i++) contexts[i].Reset();");
            }
            w.Line();

            // Filled by entity-index generation (M2). Explicit registration, no reflection.
            w.Line("partial void InitializeEntityIndices();");
        }
    }
}
