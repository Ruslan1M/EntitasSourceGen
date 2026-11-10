using NUnit.Framework;
using static EntitasSourceGen.Generator.Tests.GeneratorHarness;

namespace EntitasSourceGen.Generator.Tests;

/// <summary>Snapshot per emitter (Verify). Accept changes by replacing *.verified.cs with *.received.cs.</summary>
public class SnapshotTests
{
    private const string Components = @"
using Entitas;
using EntitasSourceGen;

[Game] public sealed class PositionComponent : IComponent { public float X; public float Y; }
[Game] public sealed class AliveComponent : IComponent { }
[Game, Unique] public sealed class ScoreComponent : IComponent { public int Value; }
[Game, Unique] public sealed class PausedComponent : IComponent { }
[Input] public sealed class HoldComponent : IComponent { }
";

    private static readonly string[] HintNames =
    {
        "Contexts.g.cs",
        "Game.Lookup.g.cs",
        "Game.Context.g.cs",
        "Game.Entity.g.cs",
        "Game.Matcher.g.cs",
        "Game.Unique.g.cs",
    };

    [TestCaseSource(nameof(HintNames))]
    public Task Emitter(string hintName)
    {
        var source = Run(Marker, ContextAttributes, Components).Source(hintName);
        return Verifier.Verify(source, "cs")
            .UseDirectory("Snapshots")
            .UseFileName(hintName.Replace(".g.cs", ""));
    }

    [Test]
    public Task Contexts_WithNamespaceAndSingleton()
    {
        var source = Run("[assembly: EntitasSourceGen.EntitasSourceGen(Namespace = \"My.Game\", Singleton = true)]",
            ContextAttributes, Components).Source("Contexts.g.cs");
        return Verifier.Verify(source, "cs")
            .UseDirectory("Snapshots")
            .UseFileName("Contexts.NamespaceSingleton");
    }
}
