using System.Collections.Generic;
using System.Linq;
using Sek.Core.Analysis;
using Sek.Core.Model;
using Sek.Core.Rendering;
using Sek.Core.Seexpl;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for the <c>Sek.Core</c> rendering, <c>.seexpl</c> serialization and graph-analysis
/// paths: labeled vs unlabeled states, accepting vs non-accepting, present vs absent initial state,
/// the large-graph HTML note, null-vs-present seexpl fields, and hash-merge root selection.
/// </summary>
public class CoreCoverageTests
{
    private static ExplorationGraph SmallGraph(bool withInitial, bool withLabels)
    {
        var g = new ExplorationGraph { Machine = withLabels ? "M" : string.Empty };
        g.InitialStateId = withInitial ? "S0" : null;
        g.States.Add(new ModelState("S0", "h0", Label: withLabels ? "start" : null, Accepting: false, Initial: withInitial));
        g.States.Add(new ModelState("S1", "h1", Label: null, Accepting: true, Initial: false));
        g.Transitions.Add(new Transition("S0", ActionInvocation.Of("T.a"), "S1"));
        return g;
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void DotRenderer_HandlesInitialAndLabels(bool withInitial, bool withLabels)
    {
        var dot = DotRenderer.Render(SmallGraph(withInitial, withLabels));
        Assert.Contains("digraph", dot);
        Assert.Contains("doublecircle", dot); // S1 accepting
        Assert.Equal(withInitial, dot.Contains("__start"));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void MermaidRenderer_HandlesInitialAndLabels(bool withInitial, bool withLabels)
    {
        var mmd = MermaidRenderer.Render(SmallGraph(withInitial, withLabels));
        Assert.Contains("stateDiagram", mmd);
        Assert.Equal(withInitial, mmd.Contains("[*] -->"));
    }

    [Fact]
    public void HtmlRenderer_EmptyTitle_And_LargeGraphNote()
    {
        var small = HtmlRenderer.Render(SmallGraph(withInitial: false, withLabels: false));
        Assert.Contains("SEK exploration", small);
        Assert.DoesNotContain("Large graph", small);

        // > 2000 transitions triggers the large-graph advisory note.
        var big = new ExplorationGraph { Machine = "Big" };
        big.InitialStateId = "S0";
        big.States.Add(new ModelState("S0", "h0", Label: null, Accepting: true, Initial: true));
        for (var i = 0; i < 2100; i++)
        {
            big.States.Add(new ModelState("S" + (i + 1), "h" + (i + 1), Label: null, Accepting: false, Initial: false));
            big.Transitions.Add(new Transition("S0", ActionInvocation.Of("T.a" + i), "S" + (i + 1)));
        }
        var html = HtmlRenderer.Render(big);
        Assert.Contains("Large graph", html);
    }

    [Fact]
    public void SeexplDocument_RoundTrip_NullAndPresentFields()
    {
        var doc = new SeexplDocument
        {
            Machine = "M",
            InitialState = "S0",
            States =
            {
                new SeexplState { Id = "S0", Hash = null, Label = null, Accepting = false, Initial = true },
                new SeexplState { Id = "S1", Hash = "h1", Label = "L", Accepting = true, Initial = false },
            },
            Transitions =
            {
                new SeexplTransition { From = "S0", To = "S1", Action = "T.a", Arguments = null, Kind = null, Result = null },
                new SeexplTransition { From = "S0", To = "S1", Action = "T.b", Arguments = new List<string> { "1" }, Kind = "event", Result = "r" },
            },
        };
        doc.Metadata["states"] = "2";

        var graph = doc.ToGraph();
        Assert.Equal(2, graph.States.Count);
        Assert.Equal(string.Empty, graph.States[0].Hash); // null hash → empty
        Assert.Equal("h1", graph.States[1].Hash);
        Assert.Empty(graph.Transitions[0].Action.Arguments); // null args → empty list
        Assert.Equal("event", graph.Transitions[1].Action.Kind);

        // FromGraph is the inverse projection.
        var back = SeexplDocument.FromGraph(graph);
        Assert.Equal("M", back.Machine);
        Assert.Equal(2, back.States.Count);
    }

    [Fact]
    public void GraphAnalysis_MergeByHash_RootSelection()
    {
        ExplorationGraph Phase(string id, string hash, bool accepting)
        {
            var g = new ExplorationGraph { Machine = "P" };
            g.States.Add(new ModelState(id, hash, Label: null, Accepting: accepting, Initial: true));
            return g;
        }

        var a = Phase("S0", "root", accepting: false);
        var b = Phase("S0", "goal", accepting: true);

        // rootHash present → the merged initial is the state with that hash.
        var mergedFound = GraphAnalysis.MergeByHash("M", new[] { a, b }, "root");
        Assert.NotNull(mergedFound.InitialStateId);

        // rootHash not present in the graphs → falls back to "S0".
        var mergedMissing = GraphAnalysis.MergeByHash("M", new[] { a, b }, "nonexistent");
        Assert.Equal("S0", mergedMissing.InitialStateId);

        // rootHash null → falls back to "S0".
        var mergedNull = GraphAnalysis.MergeByHash("M", new[] { a, b }, null);
        Assert.Equal("S0", mergedNull.InitialStateId);
    }
}
