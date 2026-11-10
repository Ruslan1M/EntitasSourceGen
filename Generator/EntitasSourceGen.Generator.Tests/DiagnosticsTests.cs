using Microsoft.CodeAnalysis;
using NUnit.Framework;
using static EntitasSourceGen.Generator.Tests.GeneratorHarness;

namespace EntitasSourceGen.Generator.Tests;

public class DiagnosticsTests
{
    private static void AssertSingle(Result result, string id, DiagnosticSeverity severity, string? messagePart = null)
    {
        var diagnostics = result.Diagnostics(id);
        Assert.That(diagnostics, Has.Length.EqualTo(1), string.Join("\n", result.GeneratorDiagnostics));
        Assert.That(diagnostics[0].Severity, Is.EqualTo(severity));
        if (messagePart is not null) Assert.That(diagnostics[0].GetMessage(), Does.Contain(messagePart));
    }

    private static void AssertNoGeneratedErrors(Result result)
        => Assert.That(result.CompileErrors.Where(e => e.Location.SourceTree?.FilePath.EndsWith(".g.cs") == true), Is.Empty,
            string.Join("\n", result.CompileErrors));

    [Test]
    public void ValidInput_HasNoDiagnostics()
    {
        var result = Run(Marker, ContextAttributes, "[Game] public sealed class AComponent : Entitas.IComponent { }");
        Assert.That(result.GeneratorDiagnostics, Is.Empty);
    }

    [Test]
    public void ECG001_ComponentWithoutContext()
    {
        var result = Run(Marker, ContextAttributes, "public sealed class LostComponent : Entitas.IComponent { }");
        AssertSingle(result, "ECG001", DiagnosticSeverity.Warning, "LostComponent");
        Assert.That(result.Diagnostics("ECG001")[0].Location.GetLineSpan().Path, Does.EndWith(".cs"));
    }

    [Test]
    public void ECG002_DuplicateNameInContext()
    {
        var result = Run(Marker, ContextAttributes,
            "namespace A { [Game] public sealed class HpComponent : Entitas.IComponent { } }",
            "namespace B { [Game] public sealed class HpComponent : Entitas.IComponent { } }");
        AssertSingle(result, "ECG002", DiagnosticSeverity.Error, "'Hp'");
        AssertNoGeneratedErrors(result);
    }

    [TestCase("public sealed class Outer { [Game] public sealed class InnerComponent : Entitas.IComponent { } }")]
    [TestCase("[Game] public sealed class GenComponent<T> : Entitas.IComponent { public T Value; }")]
    [TestCase("[Game] internal sealed class HiddenComponent : Entitas.IComponent { }")]
    [TestCase("internal class Secret { } [Game] public sealed class LeakComponent : Entitas.IComponent { public Secret Value; }")]
    public void ECG003_UnsupportedShape(string source)
    {
        var result = Run(Marker, ContextAttributes, source);
        AssertSingle(result, "ECG003", DiagnosticSeverity.Error);
        AssertNoGeneratedErrors(result);
    }

    [Test]
    public void ECG004_UniqueInSeveralContexts()
    {
        var result = Run(Marker, ContextAttributes, "[Game, Input, EntitasSourceGen.Unique] public sealed class ClockComponent : Entitas.IComponent { }");
        AssertSingle(result, "ECG004", DiagnosticSeverity.Error, "Game, Input");
    }

    [Test]
    public void ECG005_ComponentWithoutMarker()
    {
        var result = Run(ContextAttributes, "[Game] public sealed class AComponent : Entitas.IComponent { }");
        AssertSingle(result, "ECG005", DiagnosticSeverity.Warning, "AComponent");
        Assert.That(result.Sources, Is.Empty);
    }

    [Test]
    public void NoMarker_PlainComponent_IsSilent()
    {
        var result = Run("public sealed class AComponent : Entitas.IComponent { }");
        Assert.That(result.GeneratorDiagnostics, Is.Empty);
    }

    [TestCase("[Game] public sealed class TotalComponentsComponent : Entitas.IComponent { }", "TotalComponents")]
    [TestCase("[Game] public sealed class AllOfComponent : Entitas.IComponent { }", "AllOf")]
    [TestCase("[Game] public sealed class EnabledComponent : Entitas.IComponent { }", "isEnabled")]
    [TestCase("[Game] public sealed class FooComponent : Entitas.IComponent { } [Game] public sealed class IsFooComponent : Entitas.IComponent { public int V; }", "isFoo")]
    public void ECG006_GeneratedNameConflict(string source, string name)
    {
        var result = Run(Marker, ContextAttributes, source);
        AssertSingle(result, "ECG006", DiagnosticSeverity.Error, $"'{name}'");
        AssertNoGeneratedErrors(result);
    }

    [TestCase("[Game] public sealed class GameMatcherComponent : Entitas.IComponent { }", "GameMatcher")]
    [TestCase("[Game] public sealed class fooComponent : Entitas.IComponent { } [Game] public sealed class FooComponent : Entitas.IComponent { }", "_fooComponent")]
    [TestCase("[Game, EntitasSourceGen.Unique] public sealed class FooComponent : Entitas.IComponent { public int V; } [Game, EntitasSourceGen.Unique] public sealed class FooEntityComponent : Entitas.IComponent { public int V; }", "fooEntity")]
    [TestCase("[Game, EntitasSourceGen.Unique] public sealed class CountComponent : Entitas.IComponent { public int V; }", "count")]
    [TestCase("[Game] public sealed class ResetComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex] public int V; }", "Reset")]
    public void ECG006_MoreConflicts(string source, string name)
    {
        var result = Run(Marker, ContextAttributes, source);
        AssertSingle(result, "ECG006", DiagnosticSeverity.Error, $"'{name}'");
        AssertNoGeneratedErrors(result);
    }

    [TestCase("[Game] public sealed class GameComponentsLookupComponent : Entitas.IComponent { }", "GameComponentsLookup")]
    [TestCase("[Game] public sealed class CellComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex] public int X; public int Y; } [Game] public sealed class CellXComponent : Entitas.IComponent { [EntitasSourceGen.PrimaryEntityIndex] public int Value; }", "CellX")]
    [TestCase("[Game] public sealed class AComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex] public int Id; [EntitasSourceGen.EntityIndex] public int id; }", "GetEntitiesWithAId")]
    [TestCase("[Game] public sealed class gameComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex] public int V; }", "game")]
    public void ECG006_IndexAndTypeConflicts(string source, string name)
    {
        var result = Run(Marker, ContextAttributes, source);
        AssertSingle(result, "ECG006", DiagnosticSeverity.Error, $"'{name}'");
        AssertNoGeneratedErrors(result);
    }

    [Test]
    public void ECG008_BothIndexAttributes()
    {
        var result = Run(Marker, ContextAttributes,
            "[Game] public sealed class AComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex, EntitasSourceGen.PrimaryEntityIndex] public int V; }");
        AssertSingle(result, "ECG008", DiagnosticSeverity.Error, "'V'");
    }

    [TestCase("public readonly int V;")]
    [TestCase("public int V { get; }")]
    [TestCase("internal int V;")]
    [TestCase("public static int V;")]
    public void ECG009_IgnoredIndex(string member)
    {
        var result = Run(Marker, ContextAttributes,
            "[Game] public sealed class AComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex] " + member + " }");
        AssertSingle(result, "ECG009", DiagnosticSeverity.Warning, "'V'");
    }

    [Test]
    public void OverriddenProperty_KeepsIndex()
    {
        var result = Run(Marker, ContextAttributes,
            "public abstract class B { [EntitasSourceGen.PrimaryEntityIndex] public virtual int V { get; set; } }",
            "[Game] public sealed class AComponent : B, Entitas.IComponent { public override int V { get; set; } }",
            "public static class U { public static GameEntity F(GameContext c) => c.GetEntityWithA(1); }");
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void ECG004_ComponentIsSkipped()
    {
        var result = Run(Marker, ContextAttributes, "[Game, Input, EntitasSourceGen.Unique] public sealed class ClockComponent : Entitas.IComponent { }");
        Assert.That(result.Source("Game.Lookup.g.cs"), Does.Not.Contain("Clock"));
        AssertNoGeneratedErrors(result);
    }

    [Test]
    public void ECG002_PointsAtSkippedDeclaration()
    {
        var result = Run(Marker, ContextAttributes,
            "namespace A { [Game] public sealed class HpComponent : Entitas.IComponent { } }",
            "namespace B { [Game] public sealed class HpComponent : Entitas.IComponent { } }");
        var diagnostic = result.Diagnostics("ECG002").Single();
        Assert.That(diagnostic.Location.GetLineSpan().Path, Is.EqualTo("Source3.cs"));
        Assert.That(result.Source("Game.Lookup.g.cs"), Does.Contain("typeof(global::A.HpComponent)"));
    }

    [Test]
    public void Diagnostics_AreDeterministic()
    {
        var sources = new[]
        {
            Marker, ContextAttributes,
            "public sealed class ZComponent : Entitas.IComponent { }",
            "public sealed class AComponent : Entitas.IComponent { }",
            "[Game] internal sealed class MComponent : Entitas.IComponent { }",
        };
        var first = Run(sources).GeneratorDiagnostics.Select(d => d.ToString()).ToList();
        var reversed = Run(sources.Reverse().ToArray()).GeneratorDiagnostics.Select(d => d.GetMessage()).ToList();

        Assert.That(first, Has.Count.EqualTo(3));
        Assert.That(reversed, Is.EqualTo(Run(sources).GeneratorDiagnostics.Select(d => d.GetMessage()).ToList()));
    }

    [Test]
    public void ECG007_MarkerWithoutContexts()
    {
        var result = Run(Marker, "public class Nothing { }");
        AssertSingle(result, "ECG007", DiagnosticSeverity.Warning);
        Assert.That(result.Sources, Is.Empty);
    }

    [Test]
    public void PartialComponent_ReportedOnce()
    {
        var result = Run(Marker, ContextAttributes,
            "public sealed partial class LostComponent : Entitas.IComponent { }",
            "public sealed partial class LostComponent : Entitas.IComponent { public int V; }");
        AssertSingle(result, "ECG001", DiagnosticSeverity.Warning);
    }
}
