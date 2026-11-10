using NUnit.Framework;
using static EntitasSourceGen.Generator.Tests.GeneratorHarness;

namespace EntitasSourceGen.Generator.Tests;

public class CleanupAndAttributesTests
{
    private const string Components = @"
using Entitas;
using EntitasSourceGen;

[Game, Cleanup(CleanupMode.RemoveComponent)] public sealed class DamageComponent : IComponent { public int Value; }
[Game, Cleanup(CleanupMode.RemoveComponent)] public sealed class HitComponent : IComponent { }
[Game, Cleanup(CleanupMode.DestroyEntity)] public sealed class ExpiredComponent : IComponent { }
[Game, FlagPrefix(""flag"")] public sealed class DirtyComponent : IComponent { }
[Game, FlagPrefix(""can""), Unique] public sealed class MoveComponent : IComponent { }
[Game, DontGenerate] public sealed class HiddenComponent : IComponent { public int Value; }
[Game, DontGenerate(false)] public sealed class IgnoredComponent : IComponent { public int Value; }
";

    private const string Usage = @"
public static class Usage
{
    public static void Run(Contexts contexts)
    {
        Feature cleanup = new GameCleanupSystems(contexts);
        cleanup.Cleanup();
        Entitas.ICleanupSystem s1 = new RemoveDamageGameSystem(contexts);
        Entitas.ICleanupSystem s2 = new RemoveHitGameSystem(contexts);
        Entitas.ICleanupSystem s3 = new DestroyExpiredGameSystem(contexts);

        var e = contexts.game.CreateEntity();
        e.flagDirty = true;
        contexts.game.canMove = true;
        int hidden = GameComponentsLookup.Hidden;
        var m = GameMatcher.Hidden;
    }
}
";

    [Test]
    public void Compiles()
    {
        var result = Run(Marker, ContextAttributes, Components, Usage);
        Assert.That(result.GeneratorDiagnostics, Is.Empty, string.Join("\n", result.GeneratorDiagnostics));
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void DontGenerate_KeepsIndexButNoApi()
    {
        var result = Run(Marker, ContextAttributes, Components);
        Assert.That(result.Source("Game.Lookup.g.cs"), Does.Contain("Hidden"));
        Assert.That(result.Source("Game.Lookup.g.cs"), Does.Not.Contain("Ignored"));
        Assert.That(result.Source("Game.Entity.g.cs"), Does.Not.Contain("AddHidden"));
        Assert.That(result.Source("Game.Entity.g.cs"), Does.Not.Contain("isDirty"));
    }

    [Test]
    public void DontGenerate_Ignored_HasNoDiagnostics()
    {
        var result = Run(Marker, ContextAttributes, "[EntitasSourceGen.DontGenerate(false)] public sealed class XComponent : Entitas.IComponent { }");
        Assert.That(result.GeneratorDiagnostics.Where(d => d.Id != "ECG007"), Is.Empty);
    }

    [Test]
    public void ECG006_ExistingCleanupSystem()
    {
        var result = Run(Marker, ContextAttributes,
            "[Game, EntitasSourceGen.Cleanup(EntitasSourceGen.CleanupMode.RemoveComponent)] public sealed class HitComponent : Entitas.IComponent { }",
            "public sealed class RemoveHitGameSystem { }");
        Assert.That(result.GeneratorDiagnostics.Single(d => d.Id == "ECG006").GetMessage(), Does.Contain("RemoveHitGameSystem"));
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [TestCase("")]
    [TestCase("1x")]
    [TestCase("my-prefix")]
    public void ECG010_InvalidFlagPrefix(string prefix)
    {
        var result = Run(Marker, ContextAttributes,
            $"[Game, EntitasSourceGen.FlagPrefix(\"{prefix}\")] public sealed class OnComponent : Entitas.IComponent {{ }}",
            "public static class U { public static void R(GameEntity e) { e.isOn = true; } }");
        Assert.That(result.GeneratorDiagnostics.Single(d => d.Id == "ECG010").GetMessage(), Does.Contain("[FlagPrefix]"));
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void ECG010_DontGenerateIgnoresOtherAttributes()
    {
        var result = Run(Marker, ContextAttributes, @"
[Game, Input, EntitasSourceGen.DontGenerate, EntitasSourceGen.Unique, EntitasSourceGen.Cleanup(EntitasSourceGen.CleanupMode.RemoveComponent),
 EntitasSourceGen.Event(EntitasSourceGen.EventTarget.Self)]
public sealed class XComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex, EntitasSourceGen.PrimaryEntityIndex] public int V; }");

        var ids = result.GeneratorDiagnostics.Select(d => d.Id).ToList();
        Assert.That(ids, Has.Exactly(4).EqualTo("ECG010"), string.Join("\n", result.GeneratorDiagnostics));
        Assert.That(ids, Has.None.EqualTo("ECG004").And.None.EqualTo("ECG008"));
        Assert.That(result.Source("Game.Lookup.g.cs"), Does.Contain("public const int X = 0;"));
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void FlagPrefixFormingKeyword_IsEscaped()
    {
        var result = Run(Marker, ContextAttributes,
            "[Game, EntitasSourceGen.FlagPrefix(\"for\"), EntitasSourceGen.Cleanup(EntitasSourceGen.CleanupMode.RemoveComponent)] public sealed class eachComponent : Entitas.IComponent { }",
            "public static class U { public static void R(GameEntity e) { e.@foreach = true; } }");
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void EventPayload_DuplicateNamesAfterRename()
    {
        var result = Run(Marker, ContextAttributes,
            "[Game, EntitasSourceGen.Event(EntitasSourceGen.EventTarget.Self)] public sealed class LinkComponent : Entitas.IComponent { public int Entity; public int EntityValue; }");
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public Task Snapshot()
        => Verifier.Verify(Run(Marker, ContextAttributes, Components).Source("Game.Cleanup.g.cs"), "cs")
            .UseDirectory("Snapshots")
            .UseFileName("Cleanup");
}
