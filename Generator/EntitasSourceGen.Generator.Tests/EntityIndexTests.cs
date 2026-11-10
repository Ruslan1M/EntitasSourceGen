using NUnit.Framework;
using static EntitasSourceGen.Generator.Tests.GeneratorHarness;

namespace EntitasSourceGen.Generator.Tests;

public class EntityIndexTests
{
    private const string Components = @"
using Entitas;
using EntitasSourceGen;

[Game] public sealed class PlayerIndexComponent : IComponent { [PrimaryEntityIndex] public int Value; }
[Game] public sealed class TeamComponent : IComponent { [EntityIndex] public string Value; }
[Game, Input] public sealed class CellComponent : IComponent { [EntityIndex] public int X; [EntityIndex] public int Y { get; set; } }
";

    private const string Usage = @"
public static class Usage
{
    public static void Run(Contexts contexts)
    {
        GameEntity p = contexts.game.GetEntityWithPlayerIndex(1);
        System.Collections.Generic.HashSet<GameEntity> team = contexts.game.GetEntitiesWithTeam(""red"");
        var byX = contexts.game.GetEntitiesWithCellX(1);
        var byY = contexts.input.GetEntitiesWithCellY(2);
        string n = Contexts.PlayerIndex + Contexts.CellX + Contexts.CellY + Contexts.Team;
    }
}
";

    [Test]
    public void Indices_Compile()
    {
        var result = Run(Marker, ContextAttributes, Components, Usage);
        Assert.That(result.GeneratorDiagnostics, Is.Empty);
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void Indices_AreRegisteredExplicitly()
    {
        var result = Run(Marker, ContextAttributes, Components);
        var registration = result.Source("Contexts.EntityIndices.g.cs");

        Assert.That(registration, Does.Contain("partial void InitializeEntityIndices()"));
        Assert.That(registration, Does.Contain("game.AddEntityIndex(new global::Entitas.PrimaryEntityIndex<global::GameEntity, int>("));
        Assert.That(registration, Does.Contain("input.AddEntityIndex(new global::Entitas.EntityIndex<global::InputEntity, int>("));
        Assert.That(registration, Does.Not.Contain("PostConstructor"));
        Assert.That(result.Sources.Select(s => s.HintName), Does.Contain("Game.EntityIndices.g.cs").And.Contain("Input.EntityIndices.g.cs"));
    }

    [Test]
    public void NoIndices_NoIndexFiles()
    {
        var result = Run(Marker, ContextAttributes, "[Game] public sealed class AComponent : Entitas.IComponent { public int V; }");
        Assert.That(result.Sources.Select(s => s.HintName), Has.None.Contains("EntityIndices"));
    }

    [Test]
    public Task Snapshot()
    {
        var result = Run(Marker, ContextAttributes, Components);
        return Verifier.Verify(result.Source("Contexts.EntityIndices.g.cs") + "\n// ----\n" + result.Source("Game.EntityIndices.g.cs"), "cs")
            .UseDirectory("Snapshots")
            .UseFileName("EntityIndices");
    }
}
