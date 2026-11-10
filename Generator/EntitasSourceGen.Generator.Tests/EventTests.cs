using Microsoft.CodeAnalysis;
using NUnit.Framework;
using static EntitasSourceGen.Generator.Tests.GeneratorHarness;

namespace EntitasSourceGen.Generator.Tests;

public class EventTests
{
    private const string Components = @"
using Entitas;
using EntitasSourceGen;

[Game, Event(EventTarget.Self)] public sealed class PositionComponent : IComponent { public float X; public float Y; }
[Game, Event(EventTarget.Any), Event(EventTarget.Any, EventType.Removed, 5)] public sealed class ScoreComponent : IComponent { public int Value; }
[Game, Event(EventTarget.Self, EventType.Removed), Event(EventTarget.Self, EventType.Added, -1)] public sealed class AliveComponent : IComponent { }
[Game, Input, Event(EventTarget.Self)] public sealed class TickComponent : IComponent { public long Value; }
";

    private const string Usage = @"
public sealed class View : IPositionListener, IAnyScoreListener, IAnyScoreRemovedListener, IAliveListener, IAliveRemovedListener, IGameTickListener, IInputTickListener
{
    public void OnPosition(GameEntity entity, float x, float y) { }
    public void OnAnyScore(GameEntity entity, int value) { }
    public void OnAnyScoreRemoved(GameEntity entity) { }
    public void OnAlive(GameEntity entity) { }
    public void OnAliveRemoved(GameEntity entity) { }
    public void OnTick(GameEntity entity, long value) { }
    public void OnTick(InputEntity entity, long value) { }

    public static void Run(Contexts contexts)
    {
        var view = new View();
        var e = contexts.game.CreateEntity();
        e.AddPositionListener(view);
        e.AddAliveListener(view);
        e.AddAliveRemovedListener(view);
        e.AddGameTickListener(view);
        contexts.game.CreateEntity().AddAnyScoreListener(view);
        contexts.input.CreateEntity().AddInputTickListener(view);
        e.RemovePositionListener(view);
        e.RemovePositionListener(view, false);

        Entitas.Systems systems = new GameEventSystems(contexts);
        Feature f = new InputEventSystems(contexts);
        systems.Execute();
    }
}
";

    [Test]
    public void Events_Compile()
    {
        var result = Run(Marker, ContextAttributes, Components, Usage);
        Assert.That(result.GeneratorDiagnostics, Is.Empty, string.Join("\n", result.GeneratorDiagnostics));
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void ListenerComponents_JoinLookup()
    {
        var lookup = Run(Marker, ContextAttributes, Components).Source("Game.Lookup.g.cs");
        Assert.That(lookup, Does.Contain("PositionListener"));
        Assert.That(lookup, Does.Contain("AnyScoreRemovedListener"));
        Assert.That(lookup, Does.Contain("GameTickListener"));
    }

    [Test]
    public void EventSystems_OrderedByPriority()
    {
        var events = Run(Marker, ContextAttributes, Components).Source("Game.Events.g.cs");
        var feature = events.Substring(events.IndexOf("class GameEventSystems", System.StringComparison.Ordinal));
        var alive = feature.IndexOf("AliveEventSystem(", System.StringComparison.Ordinal);
        var anyScore = feature.IndexOf("AnyScoreEventSystem(", System.StringComparison.Ordinal);
        var anyScoreRemoved = feature.IndexOf("AnyScoreRemovedEventSystem(", System.StringComparison.Ordinal);
        Assert.That(alive, Is.LessThan(anyScore));
        Assert.That(anyScore, Is.LessThan(anyScoreRemoved));
    }

    [Test]
    public void Feature_GeneratedOnlyWithoutUserFeature()
    {
        Assert.That(Run(Marker, ContextAttributes, Components).Sources.Select(s => s.HintName), Does.Contain("Feature.g.cs"));

        var own = Run(Marker, ContextAttributes, Components, "public class Feature : Entitas.Systems { }");
        Assert.That(own.Sources.Select(s => s.HintName), Does.Not.Contain("Feature.g.cs"));
        Assert.That(own.CompileErrors, Is.Empty, string.Join("\n", own.CompileErrors));

        var none = Run(Marker, ContextAttributes, "[Game] public sealed class AComponent : Entitas.IComponent { }");
        Assert.That(none.Sources.Select(s => s.HintName), Does.Not.Contain("Feature.g.cs"));
    }

    [Test]
    public void ECG006_ExistingListenerType()
    {
        var result = Run(Marker, ContextAttributes,
            "[Game, EntitasSourceGen.Event(EntitasSourceGen.EventTarget.Self)] public sealed class HpComponent : Entitas.IComponent { public int V; }",
            "public interface IHpListener { }");

        var diagnostic = result.GeneratorDiagnostics.Single(d => d.Id == "ECG006");
        Assert.That(diagnostic.GetMessage(), Does.Contain("IHpListener"));
        Assert.That(result.CompileErrors.Where(e => e.Location.SourceTree?.FilePath.EndsWith(".g.cs") == true), Is.Empty,
            string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void ECG006_UserComponentNamedLikeListener()
    {
        var result = Run(Marker, ContextAttributes,
            "[Game, EntitasSourceGen.Event(EntitasSourceGen.EventTarget.Self)] public sealed class HpComponent : Entitas.IComponent { public int V; }",
            "[Game] public sealed class HpListenerComponent : Entitas.IComponent { public int V; }");

        Assert.That(result.GeneratorDiagnostics.Where(d => d.Id == "ECG006"), Has.Exactly(1).Items);
        Assert.That(result.CompileErrors.Where(e => e.Location.SourceTree?.FilePath.EndsWith(".g.cs") == true), Is.Empty,
            string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void ECG006_SameListenerAcrossContexts()
    {
        var result = Run(Marker, ContextAttributes,
            "namespace A { [Game, EntitasSourceGen.Event(EntitasSourceGen.EventTarget.Self)] public sealed class PosComponent : Entitas.IComponent { public int V; } }",
            "namespace B { [Input, EntitasSourceGen.Event(EntitasSourceGen.EventTarget.Self)] public sealed class PosComponent : Entitas.IComponent { public int V; } }");

        Assert.That(result.GeneratorDiagnostics.Where(d => d.Id == "ECG006"), Has.Exactly(1).Items, string.Join("\n", result.GeneratorDiagnostics));
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void PayloadNamedEntity_IsRenamed()
    {
        var result = Run(Marker, ContextAttributes,
            "[Game, EntitasSourceGen.Event(EntitasSourceGen.EventTarget.Any)] public sealed class LinkComponent : Entitas.IComponent { public int Entity; }");
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
        Assert.That(result.Source("Game.Events.g.cs"), Does.Contain("OnAnyLink(global::GameEntity entity, int entityValue)"));
    }

    [Test]
    public void FlagPrefix_AppliesToEvents()
    {
        var result = Run(Marker, ContextAttributes,
            "[Game, EntitasSourceGen.FlagPrefix(\"flag\"), EntitasSourceGen.Event(EntitasSourceGen.EventTarget.Self)] public sealed class OnComponent : Entitas.IComponent { }",
            "public static class U { public static void R(GameEntity e) { e.flagOn = true; } }");
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public Task Snapshot()
    {
        var result = Run(Marker, ContextAttributes, Components);
        return Verifier.Verify(result.Source("Game.Events.g.cs") + "\n// ----\n" + result.Source("Feature.g.cs"), "cs")
            .UseDirectory("Snapshots")
            .UseFileName("Events");
    }
}
