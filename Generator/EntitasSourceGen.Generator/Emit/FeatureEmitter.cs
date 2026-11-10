using EntitasSourceGen.Generator.Config;

namespace EntitasSourceGen.Generator.Emit;

/// <summary>
/// <c>Feature.g.cs</c> (Jenny parity): in the Editor a <c>DebugSystems</c> (systems tree and timings in Visual
/// Debugging), in development builds a profiled <c>Systems</c>, otherwise a plain <c>Systems</c>.
/// Each variant is emitted only if the assembly references the types it needs.
/// </summary>
internal static class FeatureEmitter
{
    private const string Summary = "/// <summary>Group of systems. Declare your own <c>Feature</c> in this namespace to replace it.</summary>";

    public static void Emit(CodeWriter w, GeneratorConfig config)
    {
        var branches = 0;
        if (config.VisualDebugging)
        {
            w.Raw("#if UNITY_EDITOR && !ENTITAS_DISABLE_VISUAL_DEBUGGING");
            EmitDebug(w);
            branches++;
        }

        if (config.Profiler)
        {
            w.Raw(branches == 0
                ? "#if DEVELOPMENT_BUILD && !ENTITAS_DISABLE_DEEP_PROFILING"
                : "#elif DEVELOPMENT_BUILD && !ENTITAS_DISABLE_DEEP_PROFILING");
            EmitProfiled(w);
            branches++;
        }

        if (branches > 0) w.Raw("#else");
        EmitPlain(w);
        if (branches > 0) w.Raw("#endif");
    }

    private static void EmitDebug(CodeWriter w)
    {
        w.Line(Summary);
        using (w.Block("public class Feature : global::Entitas.VisualDebugging.Unity.DebugSystems"))
        {
            w.Line("public Feature(string name) : base(name) { }");
            w.Line();
            w.Line("/// <summary>Named after the type: <c>MovementFeature</c> → \"Movement Feature\".</summary>");
            w.Line("public Feature() : base(true) { initialize(ReadableName(GetType().Name)); }");
            w.Line();
            using (w.Block("static string ReadableName(string name)"))
            {
                w.Line("var tick = name.IndexOf('`');");
                w.Line("if (tick >= 0) name = name.Substring(0, tick);");
                w.Line("var sb = new global::System.Text.StringBuilder(name.Length + 8);");
                using (w.Block("for (var i = 0; i < name.Length; i++)"))
                {
                    w.Line("var c = name[i];");
                    w.Line("var wordStart = i > 0 && char.IsUpper(c)");
                    w.Line("    && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1])));");
                    w.Line("if (wordStart) sb.Append(' ');");
                    w.Line("sb.Append(c);");
                }
                w.Line("return sb.ToString();");
            }
        }
    }

    private static void EmitProfiled(CodeWriter w)
    {
        const string list = "global::System.Collections.Generic.List<string>";
        const string profiler = "global::UnityEngine.Profiling.Profiler";
        w.Line(Summary);
        using (w.Block("public class Feature : global::Entitas.Systems"))
        {
            w.Line($"readonly {list} _initializeSystemNames = new {list}();");
            w.Line($"readonly {list} _executeSystemNames = new {list}();");
            w.Line($"readonly {list} _cleanupSystemNames = new {list}();");
            w.Line($"readonly {list} _tearDownSystemNames = new {list}();");
            w.Line();
            w.Line("public Feature(string name) { }");
            w.Line();
            w.Line("public Feature() { }");
            w.Line();
            using (w.Block("public override global::Entitas.Systems Add(global::Entitas.ISystem system)"))
            {
                w.Line("var name = system.GetType().FullName;");
                w.Line("if (system is global::Entitas.IInitializeSystem) _initializeSystemNames.Add(name);");
                w.Line("if (system is global::Entitas.IExecuteSystem) _executeSystemNames.Add(name);");
                w.Line("if (system is global::Entitas.ICleanupSystem) _cleanupSystemNames.Add(name);");
                w.Line("if (system is global::Entitas.ITearDownSystem) _tearDownSystemNames.Add(name);");
                w.Line("return base.Add(system);");
            }

            foreach (var (method, systems, names) in new[]
                     {
                         ("Initialize", "_initializeSystems", "_initializeSystemNames"),
                         ("Execute", "_executeSystems", "_executeSystemNames"),
                         ("Cleanup", "_cleanupSystems", "_cleanupSystemNames"),
                         ("TearDown", "_tearDownSystems", "_tearDownSystemNames"),
                     })
            {
                w.Line();
                using (w.Block($"public override void {method}()"))
                {
                    using (w.Block($"for (var i = 0; i < {systems}.Count; i++)"))
                    {
                        w.Line($"{profiler}.BeginSample({names}[i]);");
                        w.Line($"{systems}[i].{method}();");
                        w.Line($"{profiler}.EndSample();");
                    }
                }
            }
        }
    }

    private static void EmitPlain(CodeWriter w)
    {
        w.Line(Summary);
        using (w.Block("public class Feature : global::Entitas.Systems"))
        {
            w.Line("public Feature(string name) { }");
            w.Line();
            w.Line("public Feature() { }");
        }
    }
}
