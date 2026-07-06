using System.Collections.Generic;
using System.Linq;
using SpecExplorerKit.Components.Graphs;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Direct coverage for the generic <see cref="Reachability"/> component (ARC002 extraction of the
/// pure directed-graph algorithms out of <c>Sek.Core.Analysis.GraphAnalysis</c>).
/// </summary>
public class ReachabilityTests
{
    // 0 -> 1 -> 2 -> 3 ; 1 -> 4 (a side branch that never reaches 3)
    private static readonly (int, int)[] Edges =
    {
        (0, 1), (1, 2), (2, 3), (1, 4),
    };

    [Fact]
    public void Backward_FindsEveryNodeThatCanReachTheSeed()
    {
        var canReach3 = Reachability.Backward(Edges, new[] { 3 });
        Assert.Equal(new[] { 0, 1, 2, 3 }, canReach3.OrderBy(x => x));
        Assert.DoesNotContain(4, canReach3);
    }

    [Fact]
    public void Forward_FindsEveryNodeReachableFromTheSeed()
    {
        var from1 = Reachability.Forward(Edges, new[] { 1 });
        Assert.Equal(new[] { 1, 2, 3, 4 }, from1.OrderBy(x => x));
        Assert.DoesNotContain(0, from1);
    }

    [Fact]
    public void Seeds_AreAlwaysIncluded_EvenWithNoEdges()
    {
        var back = Reachability.Backward(System.Array.Empty<(int, int)>(), new[] { 7 });
        var fwd = Reachability.Forward(System.Array.Empty<(int, int)>(), new[] { 7 });
        Assert.Equal(new[] { 7 }, back);
        Assert.Equal(new[] { 7 }, fwd);
    }

    [Fact]
    public void Backward_HandlesCycles_WithoutLooping()
    {
        // 0 -> 1 -> 2 -> 0 (cycle), seed = 2
        var edges = new[] { (0, 1), (1, 2), (2, 0) };
        var canReach2 = Reachability.Backward(edges, new[] { 2 });
        Assert.Equal(new[] { 0, 1, 2 }, canReach2.OrderBy(x => x));
    }

    [Fact]
    public void Comparer_IsHonoured_ForReferenceNodes()
    {
        // string nodes with an ordinal comparer; a diamond 0 -> a,b -> c
        var edges = new[] { ("s", "a"), ("s", "b"), ("a", "t"), ("b", "t") };
        var toT = Reachability.Backward(edges, new[] { "t" }, System.StringComparer.Ordinal);
        Assert.Equal(new[] { "a", "b", "s", "t" }, toT.OrderBy(x => x, System.StringComparer.Ordinal));
    }
}
