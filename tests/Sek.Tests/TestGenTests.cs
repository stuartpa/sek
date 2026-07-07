using System.Linq;
using Sek.Cli;
using Sek.Core.Model;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Direct coverage for <see cref="TestGen"/> witness-path selection: the Short (many short tests)
/// and Long (greedy covering tour) strategies over a branching graph, and strategy-name parsing.
/// </summary>
public class TestGenTests
{
    // A diamond with a loop: S0 -a-> S1 -b-> S2(acc) ; S0 -c-> S3 -d-> S2 ; S2 -e-> S0
    private static ExplorationGraph Diamond()
    {
        var g = new ExplorationGraph { Machine = "M", InitialStateId = "S0" };
        g.States.Add(new ModelState("S0", "h0", Initial: true));
        g.States.Add(new ModelState("S1", "h1"));
        g.States.Add(new ModelState("S2", "h2", Accepting: true));
        g.States.Add(new ModelState("S3", "h3"));
        g.Transitions.Add(new Transition("S0", ActionInvocation.Of("a"), "S1"));
        g.Transitions.Add(new Transition("S1", ActionInvocation.Of("b"), "S2"));
        g.Transitions.Add(new Transition("S0", ActionInvocation.Of("c"), "S3"));
        g.Transitions.Add(new Transition("S3", ActionInvocation.Of("d"), "S2"));
        g.Transitions.Add(new Transition("S2", ActionInvocation.Of("e"), "S0"));
        return g;
    }

    [Theory]
    [InlineData("shorttests", TestGen.TestStrategy.Short)]
    [InlineData("short", TestGen.TestStrategy.Short)]
    [InlineData("longtests", TestGen.TestStrategy.Long)]
    [InlineData("", TestGen.TestStrategy.Long)]
    [InlineData(null, TestGen.TestStrategy.Long)]
    [InlineData("unknown", TestGen.TestStrategy.Long)]
    public void ParseStrategy_MapsNames(string? name, TestGen.TestStrategy expected) =>
        Assert.Equal(expected, TestGen.ParseStrategy(name));

    [Fact]
    public void SelectPaths_Long_CoversAllTransitions_InFewTests()
    {
        var g = Diamond();
        var paths = TestGen.SelectPaths(g, maxTests: 100, TestGen.TestStrategy.Long);
        var covered = paths.SelectMany(p => p.Steps).Distinct().Count();
        Assert.Equal(g.Transitions.Count, covered);        // all transitions covered
        Assert.All(paths, p => Assert.Equal("S0", p.Steps.First().FromStateId)); // each starts at init
    }

    [Fact]
    public void SelectPaths_Short_ProducesManyShortWitnesses()
    {
        var g = Diamond();
        var shortPaths = TestGen.SelectPaths(g, maxTests: 100, TestGen.TestStrategy.Short);
        var longPaths = TestGen.SelectPaths(g, maxTests: 100, TestGen.TestStrategy.Long);
        // Short strategy yields at least as many (usually more) paths than Long for a branching graph.
        Assert.True(shortPaths.Count >= longPaths.Count);
        // still covers every transition
        Assert.Equal(g.Transitions.Count, shortPaths.SelectMany(p => p.Steps).Distinct().Count());
    }

    [Fact]
    public void SelectPaths_RespectsMaxTests()
    {
        var g = Diamond();
        var paths = TestGen.SelectPaths(g, maxTests: 1, TestGen.TestStrategy.Long);
        Assert.True(paths.Count <= 1);
    }

    [Fact]
    public void SelectPaths_EmptyGraph_YieldsNoPaths()
    {
        var g = new ExplorationGraph { Machine = "M", InitialStateId = "S0" };
        g.States.Add(new ModelState("S0", "h0", Initial: true, Accepting: true));
        var paths = TestGen.SelectPaths(g, maxTests: 100, TestGen.TestStrategy.Long);
        Assert.Empty(paths);
    }
}
