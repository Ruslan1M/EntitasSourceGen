namespace EntitasSourceGen.Generator.Emit;

/// <summary><c>GameMatcher</c>: cached per-component matchers + AllOf / AnyOf.</summary>
internal static class MatcherEmitter
{
    public static void Emit(CodeWriter w, Scope s)
    {
        var iMatcher = $"global::Entitas.IMatcher<{s.EntityRef}>";
        var matcher = $"global::Entitas.Matcher<{s.EntityRef}>";

        using (w.Block($"public sealed partial class {s.Matcher}"))
        {
            w.Line($"public static global::Entitas.IAllOfMatcher<{s.EntityRef}> AllOf(params int[] indices) {{ return {matcher}.AllOf(indices); }}");
            w.Line($"public static global::Entitas.IAllOfMatcher<{s.EntityRef}> AllOf(params {iMatcher}[] matchers) {{ return {matcher}.AllOf(matchers); }}");
            w.Line($"public static global::Entitas.IAnyOfMatcher<{s.EntityRef}> AnyOf(params int[] indices) {{ return {matcher}.AnyOf(indices); }}");
            w.Line($"public static global::Entitas.IAnyOfMatcher<{s.EntityRef}> AnyOf(params {iMatcher}[] matchers) {{ return {matcher}.AnyOf(matchers); }}");

            foreach (var cc in s.Ctx.Components)
            {
                var c = cc.Component;
                var field = "_matcher" + c.Name;
                w.Line();
                w.Line($"static {iMatcher} {field};");
                w.Line();
                using (w.Block($"public static {iMatcher} {c.Id}"))
                {
                    using (w.Block("get"))
                    {
                        using (w.Block($"if ({field} == null)"))
                        {
                            w.Line($"var m = ({matcher}){matcher}.AllOf({s.IndexRef(c)});");
                            w.Line($"m.componentNames = {s.LookupRef}.componentNames;");
                            w.Line($"{field} = m;");
                        }
                        w.Line();
                        w.Line($"return {field};");
                    }
                }
            }
        }
    }
}
