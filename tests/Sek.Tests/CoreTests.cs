using System.IO;
using System.Linq;
using Sek.Core.Analysis;
using Sek.Core.Model;
using Sek.Core.Rendering;
using Sek.Core.Seexpl;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for <c>Sek.Core</c>: the exploration-graph IR, the DOT/Mermaid/HTML renderers, the
/// <c>.seexpl</c> serialization round-trip, and the <see cref="GraphAnalysis"/> domain adapter
/// over the generic reachability component (readiness-gate coverage drive).
/// </summary>
public class CoreTests
{
    private static ExplorationGraph Sample()
    {
        // S0 --A--> S1 --B--> S2(accepting) ; S1 --C--> S3 (dead end)
        var g = new ExplorationGraph { Machine = "M", InitialStateId = "S0" };
        g.States.Add(new ModelState("S0", "h0", Initial: true));
        g.States.Add(new ModelState("S1", "h1"));
        g.States.Add(new ModelState("S2", "h2", Accepting: true));
        g.States.Add(new ModelState("S3", "h3"));
        g.Transitions.Add(new Transition("S0", ActionInvocation.Of("A"), "S1"));
        g.Transitions.Add(new Transition("S1", new ActionInvocation("B", new[] { "1" }), "S2"));
        g.Transitions.Add(new Transition("S1", ActionInvocation.Of("C"), "S3"));
        return g;
    }

    // ---- Model IR ----------------------------------------------------------------------

    [Fact]
    public void ActionInvocation_Display_IsEvent_Of()
    {
        Assert.Equal("A", ActionInvocation.Of("A").Display);
        Assert.Equal("Write(1, x)", new ActionInvocation("Write", new[] { "1", "x" }).Display);
        Assert.True(new ActionInvocation("E", new string[0], "event").IsEvent);
        Assert.False(ActionInvocation.Of("A").IsEvent);
        Assert.Equal("Write(1, x)", new ActionInvocation("Write", new[] { "1", "x" }).ToString());
    }

    [Fact]
    public void ExplorationGraph_FindState_And_OutgoingFrom()
    {
        var g = Sample();
        Assert.Equal("h1", g.FindState("S1")!.Hash);
        Assert.Null(g.FindState("nope"));
        Assert.Equal(2, g.OutgoingFrom("S1").Count());
        Assert.Empty(g.OutgoingFrom("S2"));
    }

    // ---- Renderers ---------------------------------------------------------------------

    [Fact]
    public void MermaidRenderer_EmitsInitialAcceptingAndTransitions()
    {
        var mmd = MermaidRenderer.Render(Sample());
        Assert.Contains("stateDiagram-v2", mmd);
        Assert.Contains("[*] --> S0", mmd);
        Assert.Contains("S2 --> [*]", mmd);       // accepting
        Assert.Contains("S0 --> S1 : A", mmd);
        Assert.Contains("B(1)", mmd);
    }

    [Fact]
    public void DotRenderer_EmitsDigraphDoublecircleAndStart()
    {
        var dot = DotRenderer.Render(Sample());
        Assert.Contains("digraph \"M\"", dot);
        Assert.Contains("rankdir=LR", dot);
        Assert.Contains("doublecircle", dot);      // accepting state
        Assert.Contains("__start -> \"S0\"", dot);
    }

    [Fact]
    public void HtmlRenderer_EmitsSelfContainedPageWithMermaidAndCounts()
    {
        var html = HtmlRenderer.Render(Sample());
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("mermaid", html);
        Assert.Contains("4 states, 3 transitions", html);
    }

    // ---- Seexpl round-trip -------------------------------------------------------------

    [Fact]
    public void Seexpl_FromGraph_ToGraph_RoundTrips()
    {
        var g = Sample();
        var doc = SeexplDocument.FromGraph(g);
        var back = doc.ToGraph();
        Assert.Equal(g.Machine, back.Machine);
        Assert.Equal(g.InitialStateId, back.InitialStateId);
        Assert.Equal(g.States.Count, back.States.Count);
        Assert.Equal(g.Transitions.Count, back.Transitions.Count);
        Assert.Contains(back.States, s => s.Id == "S2" && s.Accepting);
        Assert.Contains(back.Transitions, t => t.Action.Display == "B(1)");
    }

    [Fact]
    public void Seexpl_Save_Load_RoundTripsOnDisk()
    {
        var g = Sample();
        var path = Path.Combine(Path.GetTempPath(), $"sek_{System.Guid.NewGuid():N}.seexpl");
        try
        {
            SeexplDocument.FromGraph(g).Save(path);
            var loaded = SeexplDocument.Load(path);
            Assert.Equal("M", loaded.Machine);
            Assert.Equal(4, loaded.States.Count);
            Assert.Contains("stateDiagram", MermaidRenderer.Render(loaded.ToGraph())); // usable graph
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Seexpl_ToJson_IncludesSchemaFields()
    {
        var json = SeexplDocument.FromGraph(Sample()).ToJson();
        Assert.Contains("\"seexplVersion\"", json);
        Assert.Contains("\"machine\": \"M\"", json);
        Assert.Contains("\"transitions\"", json);
    }

    // ---- GraphAnalysis (domain adapter over Components.Graphs) --------------------------

    [Fact]
    public void GraphAnalysis_FilterToReaching_KeepsOnlyPathsToTarget()
    {
        var g = Sample();
        var targets = GraphAnalysis.FilterToReaching(g, s => s.Accepting); // only S2
        Assert.Equal(1, targets);
        Assert.Contains(g.States, s => s.Id == "S0"); // initial retained
        Assert.Contains(g.States, s => s.Id == "S2");
        Assert.DoesNotContain(g.States, s => s.Id == "S3"); // dead end pruned
    }

    [Fact]
    public void GraphAnalysis_MergeByHash_UnifiesStatesAcrossGraphs()
    {
        var g1 = Sample();
        // a second graph that shares hash h2 and adds h4 reachable from it
        var g2 = new ExplorationGraph { Machine = "M", InitialStateId = "S2b" };
        g2.States.Add(new ModelState("S2b", "h2", Accepting: true));
        g2.States.Add(new ModelState("S4b", "h4"));
        g2.Transitions.Add(new Transition("S2b", ActionInvocation.Of("D"), "S4b"));

        var merged = GraphAnalysis.MergeByHash("M", new[] { g1, g2 }, rootHash: "h0");
        // hashes h0..h4 unified into 5 states
        Assert.Equal(5, merged.States.Count);
        Assert.Contains(merged.Transitions, t => t.Action.Display == "D");
    }

    [Fact]
    public void GraphAnalysis_FilterToGoalThenAccepting_KeepsRootToGoalToAccept()
    {
        // S0 -> S1(goal) -> S2(accepting) ; S0 -> S3 (off-path)
        var g = new ExplorationGraph { Machine = "M", InitialStateId = "S0" };
        g.States.Add(new ModelState("S0", "h0", Initial: true));
        g.States.Add(new ModelState("S1", "hg"));
        g.States.Add(new ModelState("S2", "h2", Accepting: true));
        g.States.Add(new ModelState("S3", "h3"));
        g.Transitions.Add(new Transition("S0", ActionInvocation.Of("A"), "S1"));
        g.Transitions.Add(new Transition("S1", ActionInvocation.Of("B"), "S2"));
        g.Transitions.Add(new Transition("S0", ActionInvocation.Of("X"), "S3"));

        GraphAnalysis.FilterToGoalThenAccepting(g, new System.Collections.Generic.HashSet<string> { "hg" });
        Assert.Contains(g.States, s => s.Id == "S1"); // goal kept
        Assert.Contains(g.States, s => s.Id == "S2"); // accepting completion kept
        Assert.DoesNotContain(g.States, s => s.Id == "S3"); // off-path pruned
    }
}
