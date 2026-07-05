using Sek.Cli;
using Sek.Core.Model;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Item 4 — test-generation strategies (<c>shorttests</c> / <c>longtests</c>). Verifies
/// <see cref="TestGen.ParseStrategy"/> and that the two strategies differ as expected while
/// both fully cover the transition system.
/// </summary>
public class TestStrategyTests
{
    [Theory]
    [InlineData("shorttests", TestGen.TestStrategy.Short)]
    [InlineData("short", TestGen.TestStrategy.Short)]
    [InlineData("longtests", TestGen.TestStrategy.Long)]
    [InlineData("LongTests", TestGen.TestStrategy.Long)]
    [InlineData(null, TestGen.TestStrategy.Long)]
    [InlineData("bogus", TestGen.TestStrategy.Long)]
    public void ParseStrategy_MapsNames(string? name, TestGen.TestStrategy expected) =>
        Assert.Equal(expected, TestGen.ParseStrategy(name));

    // A 4-cycle where every state is accepting, so short tests can stop immediately.
    private static ExplorationGraph Cycle4()
    {
        var g = new ExplorationGraph { InitialStateId = "S0" };
        for (var i = 0; i < 4; i++) g.States.Add(new ModelState("S" + i, "h" + i, Accepting: true, Initial: i == 0));
        void T(int a, string act, int b) => g.Transitions.Add(new Transition("S" + a, new ActionInvocation(act, System.Array.Empty<string>()), "S" + b));
        T(0, "a", 1); T(1, "b", 2); T(2, "c", 3); T(3, "d", 0);
        return g;
    }

    private static int CoveredCount(ExplorationGraph g, System.Collections.Generic.List<TestGen.TestPath> paths)
    {
        var covered = new System.Collections.Generic.HashSet<Transition>();
        foreach (var p in paths) foreach (var s in p.Steps) covered.Add(s);
        return covered.Count;
    }

    [Fact]
    public void LongStrategy_ProducesFewLongPaths_CoveringAll()
    {
        var g = Cycle4();
        var paths = TestGen.SelectPaths(g, maxTests: 50, TestGen.TestStrategy.Long);
        Assert.Single(paths);                       // one long covering tour
        Assert.Equal(4, paths[0].Steps.Count);      // traverses the whole cycle
        Assert.Equal(4, CoveredCount(g, paths));    // covers all transitions
    }

    [Fact]
    public void ShortStrategy_ProducesManyShortPaths_CoveringAll()
    {
        var g = Cycle4();
        var paths = TestGen.SelectPaths(g, maxTests: 50, TestGen.TestStrategy.Short);
        Assert.True(paths.Count > 1);                                   // many small tests
        Assert.Equal(4, CoveredCount(g, paths));                        // still covers all
        var longPaths = TestGen.SelectPaths(g, 50, TestGen.TestStrategy.Long);
        Assert.True(paths.Count > longPaths.Count);                     // more tests than long
        Assert.True(paths.Min(p => p.Steps.Count) <= longPaths.Max(p => p.Steps.Count)); // and shorter
    }
}
