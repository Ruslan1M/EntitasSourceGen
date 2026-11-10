using NUnit.Framework;
using static EntitasSourceGen.Generator.Tests.GeneratorHarness;

namespace EntitasSourceGen.Generator.Tests;

/// <summary>1.1: Value shortcuts, fluent API, shared-component interfaces, composite indices, Feature variants.</summary>
public class ApiExtensionsTests
{
    private const string Components = @"
using Entitas;
using EntitasSourceGen;

[Game] public sealed class Speed : IComponent { public float Value; }
[Game] public sealed class PositionComponent : IComponent { public float X; public float Y; }
[Game, Unique] public sealed class HeroComponent : IComponent { public int Value; }
[Game, Input] public sealed class IdComponent : IComponent { [PrimaryEntityIndex] public int Value; }
[Game, Input] public sealed class DestructedComponent : IComponent { }
[Game] public sealed class DestroyComponent : IComponent { public int Value; }
";

    private const string Usage = @"
public static class Usage
{
    public static GameEntity Create(Contexts contexts)
        => contexts.game.CreateEntity()
            .AddSpeed(2f)
            .AddPosition(1f, 2f)
            .ReplaceSpeed(3f)
            .AddId(1)
            .RemovePosition();

    public static float Read(Contexts contexts, GameEntity e)
    {
        contexts.game.SetHero(5);
        int hero = contexts.game.Hero;
        int destroy = e.destroy.Value;
        return e.Speed + hero + destroy;
    }

    // Generic code over every context that has Id / Destructed.
    public static TEntity Tag<TEntity>(TEntity e) where TEntity : IIdEntity<TEntity>, IDestructedEntity
    {
        e.isDestructed = true;
        return e.ReplaceId(e.Id + 1);
    }

    public static void Shared(GameEntity g, InputEntity i)
    {
        IIdEntity a = g;
        IIdEntity b = i;
        Entitas.IEntity added = a.AddId(1);
        int id = b.Id;
        bool has = a.hasId;
        IdComponent c = a.id;
        Tag(g);
        Tag(i);
    }
}
";

    [Test]
    public void Api_Compiles()
    {
        var result = Run(Marker, ContextAttributes, Components, Usage);
        Assert.That(result.GeneratorDiagnostics, Is.Empty);
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void ValueShortcut_OnlyForPascalCaseSingleValue_AndNotBuiltIn()
    {
        var entity = Run(Marker, ContextAttributes, Components).Source("Game.Entity.g.cs");
        Assert.That(entity, Does.Contain("public float Speed { get { return speed.Value; } }"));
        Assert.That(entity, Does.Not.Contain("public int Destroy {"));
        Assert.That(entity, Does.Not.Contain(" X { get { return"));
    }

    [Test]
    public void Interfaces_OnlyForSharedComponents()
    {
        var result = Run(Marker, ContextAttributes, Components);
        var interfaces = result.Source("Components.Interfaces.g.cs");
        Assert.That(interfaces, Does.Contain("public partial interface IIdEntity<TEntity> : global::Entitas.IEntity where TEntity : global::Entitas.IEntity"));
        Assert.That(interfaces, Does.Contain("public partial interface IIdEntity : IIdEntity<global::Entitas.IEntity> { }"));
        Assert.That(interfaces, Does.Contain("public partial interface IDestructedEntity : global::Entitas.IEntity"));
        Assert.That(interfaces, Does.Not.Contain("ISpeedEntity"));
        Assert.That(result.Source("Input.Entity.g.cs"), Does.Contain("global::IIdEntity<global::InputEntity>, global::IIdEntity"));
    }

    [Test]
    public void NoSharedComponents_NoInterfaceFile()
    {
        var result = Run(Marker, ContextAttributes, "[Game] public sealed class AComponent : Entitas.IComponent { public int Value; }");
        Assert.That(result.Sources.Select(s => s.HintName), Has.None.EqualTo("Components.Interfaces.g.cs"));
    }

    [Test]
    public void Interface_ClashingWithUserType_IsReported()
    {
        var result = Run(Marker, ContextAttributes, Components, "public interface IIdEntity { }");
        Assert.That(result.Diagnostics("ECG006").Single().GetMessage(), Does.Contain("IIdEntity"));
        Assert.That(result.Sources.Select(s => s.HintName), Does.Contain("Components.Interfaces.g.cs"));
        Assert.That(result.Source("Components.Interfaces.g.cs"), Does.Not.Contain("IIdEntity"));
    }

    private const string CompositeComponents = @"
using Entitas;
using EntitasSourceGen;

public enum StatusType { Poison, Freeze }
[Game] public sealed class TargetIdComponent : IComponent { [EntityIndex(""StatusesOfTarget"")] public int Value; }
[Game] public sealed class StatusTypeComponent : IComponent { [EntityIndex(""StatusesOfTarget"")] public StatusType Value; }
[Game, Input] public sealed class CellComponent : IComponent { [PrimaryEntityIndex(""Cell"")] public int X; [PrimaryEntityIndex(""Cell"")] public int Y; }
[Game] public sealed class OwnerComponent : IComponent { [EntityIndex(""ByOwner"")] public int Value; }
";

    private const string CompositeUsage = @"
public static class Usage
{
    public static void Run(Contexts contexts)
    {
        System.Collections.Generic.HashSet<GameEntity> statuses = contexts.game.GetEntitiesWithStatusesOfTarget(StatusType.Poison, 7);
        GameEntity cell = contexts.game.GetEntityWithCell(1, 2);
        InputEntity inputCell = contexts.input.GetEntityWithCell(1, 2);
        var owned = contexts.game.GetEntitiesWithByOwner(3);
        string n = Contexts.StatusesOfTarget + Contexts.Cell + Contexts.ByOwner;
    }
}
";

    [Test]
    public void CompositeIndex_Compiles()
    {
        var result = Run(Marker, ContextAttributes, CompositeComponents, CompositeUsage);
        Assert.That(result.GeneratorDiagnostics, Is.Empty);
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [Test]
    public void CompositeIndex_UsesTupleKeyOverOneGroup()
    {
        var registration = Run(Marker, ContextAttributes, CompositeComponents).Source("Contexts.EntityIndices.g.cs");
        Assert.That(registration, Does.Contain("new global::Entitas.EntityIndex<global::GameEntity, (global::StatusType, int)>("));
        Assert.That(registration, Does.Contain("game.GetGroup(global::GameMatcher.AllOf(global::GameMatcher.StatusType, global::GameMatcher.TargetId))"));
        Assert.That(registration, Does.Contain("(c as global::TargetIdComponent ?? (global::TargetIdComponent)e.GetComponent(global::GameComponentsLookup.TargetId)).Value"));
        Assert.That(registration, Does.Contain("new global::Entitas.PrimaryEntityIndex<global::InputEntity, (int, int)>("));
        Assert.That(registration, Does.Contain("game.GetGroup(global::GameMatcher.Cell)"));
    }

    [Test]
    public void NamedSingleIndex_KeepsClassicKey()
    {
        // Registration of a named single-member index keeps the classic getKey shape.
        var registration = Run(Marker, ContextAttributes, CompositeComponents).Source("Contexts.EntityIndices.g.cs");
        Assert.That(registration, Does.Contain("(e, c) => ((global::OwnerComponent)(c ?? e.GetComponent(global::GameComponentsLookup.Owner))).Value));"));
    }

    [Test]
    public void ECG011_MixedKinds()
    {
        var result = Run(Marker, ContextAttributes, @"
[Game] public sealed class AComponent : Entitas.IComponent { [EntitasSourceGen.PrimaryEntityIndex(""Key"")] public int Value; }
[Game] public sealed class BComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex(""Key"")] public int Value; }
");
        Assert.That(result.Diagnostics("ECG011"), Has.Length.EqualTo(1));
        Assert.That(result.Sources.Select(s => s.HintName), Has.None.Contains("EntityIndices"));
        Assert.That(result.CompileErrors, Is.Empty);
    }

    [Test]
    public void ECG012_PartsMissingInContext()
    {
        var result = Run(Marker, ContextAttributes, @"
[Game, Input] public sealed class AComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex(""Key"")] public int Value; }
[Game] public sealed class BComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex(""Key"")] public int Value; }
");
        var d = result.Diagnostics("ECG012").Single();
        Assert.That(d.GetMessage(), Does.Contain("'Input'").And.Contain("BComponent"));
        Assert.That(result.Sources.Select(s => s.HintName), Does.Contain("Game.EntityIndices.g.cs").And.Not.Contain("Input.EntityIndices.g.cs"));
        Assert.That(result.CompileErrors, Is.Empty);
    }

    [Test]
    public void NamedIndex_ClashingWithClassicIndex_IsReported()
    {
        var result = Run(Marker, ContextAttributes, @"
[Game] public sealed class TeamComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex] public int Value; }
[Game] public sealed class BComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex(""Team"")] public int Value; }
");
        Assert.That(result.Diagnostics("ECG006"), Has.Length.EqualTo(1));
        Assert.That(result.CompileErrors, Is.Empty);
    }

    [Test]
    public void InvalidIndexName_FallsBackToDefault()
    {
        var result = Run(Marker, ContextAttributes,
            @"[Game] public sealed class TeamComponent : Entitas.IComponent { [EntitasSourceGen.EntityIndex(""1 bad"")] public int Value; }");
        Assert.That(result.Diagnostics("ECG010"), Has.Length.EqualTo(1));
        Assert.That(result.Source("Game.EntityIndices.g.cs"), Does.Contain("GetEntitiesWithTeam(int value)"));
    }

    [Test]
    public void Feature_HasDebugSystemsBranch_WhenVisualDebuggingIsReferenced()
    {
        // The test references Entitas.VisualDebugging.Unity.dll, but not UnityEngine.
        var feature = Run(Marker, ContextAttributes, Components).Source("Feature.g.cs");
        Assert.That(feature, Does.Contain("#if UNITY_EDITOR && !ENTITAS_DISABLE_VISUAL_DEBUGGING"));
        Assert.That(feature, Does.Contain("public class Feature : global::Entitas.VisualDebugging.Unity.DebugSystems"));
        Assert.That(feature, Does.Not.Contain("Profiler"));
    }

    [TestCase("UNITY_EDITOR")]
    [TestCase("DEVELOPMENT_BUILD")]
    [TestCase("UNITY_EDITOR", "ENTITAS_DISABLE_VISUAL_DEBUGGING")]
    public void Feature_EveryBranch_CompilesAgainstUnity(params string[] defines)
    {
        var unity = UnityReferences();
        if (unity.Count == 0) Assert.Ignore("No local Unity install (set UNITY_MANAGED).");

        const string usage = @"
public sealed class MoveFeature : Feature { public MoveFeature() { Add(new MoveSystem()); } }
public sealed class MoveSystem : Entitas.IExecuteSystem { public void Execute() { } }
public static class FeatureUsage { public static Entitas.Systems Create() => new Feature(""Root"").Add(new MoveFeature()); }
";
        var result = RunWith(defines, unity, Marker, ContextAttributes, Components, usage);
        var feature = result.Source("Feature.g.cs");
        Assert.That(feature, Does.Contain("UnityEngine.Profiling.Profiler.BeginSample"));
        Assert.That(result.CompileErrors, Is.Empty, string.Join("\n", result.CompileErrors));
    }

    [TestCase("MovementFeature", "Movement Feature")]
    [TestCase("HUDFeature", "HUD Feature")]
    [TestCase("Feature", "Feature")]
    public void Feature_ReadableName(string type, string expected)
    {
        // Mirrors the ReadableName helper emitted into the DebugSystems Feature.
        var feature = Run(Marker, ContextAttributes, Components).Source("Feature.g.cs");
        Assert.That(feature, Does.Contain("static string ReadableName(string name)"));
        Assert.That(ReadableName(type), Is.EqualTo(expected));
    }

    private static string ReadableName(string name)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            var wordStart = i > 0 && char.IsUpper(c)
                && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1])));
            if (wordStart) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
