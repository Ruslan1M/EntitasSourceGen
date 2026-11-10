using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using static EntitasSourceGen.Generator.Tests.GeneratorHarness;

namespace EntitasSourceGen.Generator.Tests;

public class GateAndCacheTests
{
    private const string Component = "[Game] public sealed class LivesComponent : Entitas.IComponent { public int Value; }";

    private static readonly IncrementalStepRunReason[] NotRecomputed =
        { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged };

    [Test]
    public void WithoutMarker_GeneratesNothing()
    {
        var result = Run(ContextAttributes, Component);

        Assert.That(result.Sources, Is.Empty);
    }

    [Test]
    public void WithoutAttributesAssembly_GeneratesNothing()
    {
        // A compilation that does not even see EntitasSourceGen (no marker type).
        var compilation = CSharpCompilation.Create("Plain",
            new[] { CSharpSyntaxTree.ParseText("public class A { }", CSharp9Options) },
            CreateCompilation().References);
        var run = CreateDriver().RunGenerators(compilation).GetRunResult();

        Assert.That(run.Results.Single().GeneratedSources, Is.Empty);
        Assert.That(run.Results.Single().Exception, Is.Null);
    }

    [Test]
    public void SecondRun_WithoutChanges_IsCached()
    {
        var compilation = CreateCompilation(Marker, ContextAttributes, Component);
        var driver = CreateDriver(trackSteps: true).RunGenerators(compilation);

        // Same syntax, new compilation instance (what Unity / the IDE does after edits elsewhere).
        driver = driver.RunGenerators(compilation.Clone());
        var result = driver.GetRunResult().Results.Single();

        AssertReasons(result.TrackedSteps, TrackingNames.Components, IncrementalStepRunReason.Cached);
        AssertReasons(result.TrackedSteps, TrackingNames.AllComponents, IncrementalStepRunReason.Cached);
        AssertReasons(result.TrackedSteps, TrackingNames.Contexts, IncrementalStepRunReason.Cached);
        AssertReasons(result.TrackedSteps, TrackingNames.EmitInput, NotRecomputed);
        AssertReasons(result.TrackedOutputSteps, "SourceOutput", NotRecomputed);
    }

    [Test]
    public void UnrelatedEdit_KeepsEmissionCached()
    {
        var compilation = CreateCompilation(Marker, ContextAttributes, Component, "public class Other { }");
        var driver = CreateDriver(trackSteps: true).RunGenerators(compilation);

        var oldTree = compilation.SyntaxTrees.Single(t => t.ToString().Contains("class Other"));
        var newTree = oldTree.WithChangedText(SourceText.From("public class Other { public int X; }"));
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(oldTree, newTree));
        var steps = driver.GetRunResult().Results.Single().TrackedSteps;

        AssertReasons(steps, TrackingNames.AllComponents, NotRecomputed);
        AssertReasons(steps, TrackingNames.EmitInput, NotRecomputed);
    }

    [Test]
    public void MovingComponent_ReRunsOnlyDiagnostics()
    {
        var compilation = CreateCompilation(Marker, ContextAttributes, Component);
        var driver = CreateDriver(trackSteps: true).RunGenerators(compilation);

        var oldTree = compilation.SyntaxTrees.Single(t => t.ToString().Contains("LivesComponent"));
        var newTree = oldTree.WithChangedText(SourceText.From("\n\n" + Component));
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(oldTree, newTree));
        var steps = driver.GetRunResult().Results.Single().TrackedSteps;

        // The candidate (with its location) changes, the location-free model does not.
        AssertReasons(steps, TrackingNames.Components, IncrementalStepRunReason.Modified);
        AssertReasons(steps, TrackingNames.AllComponents, NotRecomputed);
        AssertReasons(steps, TrackingNames.EmitInput, NotRecomputed);
    }

    private static void AssertReasons(
        IReadOnlyDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> steps, string name, params IncrementalStepRunReason[] allowed)
    {
        Assert.That(steps.ContainsKey(name), $"step {name} not tracked");
        Assert.That(steps[name], Is.Not.Empty, name);
        foreach (var step in steps[name])
        {
            Assert.That(step.Outputs, Is.Not.Empty, name);
            foreach (var (_, reason) in step.Outputs)
                Assert.That(allowed, Does.Contain(reason), $"{name}: {reason}");
        }
    }
}
