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

    [Fact]
    public void Seexpl_SaveGraph_RoundTripsCompatibleSchema()
    {
        var graph = Sample();
        graph.States.Add(new ModelState("S4", "h4", "label"));
        graph.Transitions.Add(new Transition("S4", new ActionInvocation("UnspecifiedKind", new string[0], null!), "S0"));
        graph.Transitions.Add(new Transition("S2", new ActionInvocation("Return", new[] { "quoted\"", "unicode\u00e9" }, "return", "result"), "S4"));
        graph.NegativeTransitions.Add(new NegativeTransition("S0", ActionInvocation.Of("Denied"), "guard")
        {
            ObligationId = "permission.denied",
            ExpectedExceptionType = "System.InvalidOperationException",
            ExpectedMessageContains = "guard",
        });
        graph.NegativeTransitions.Add(new NegativeTransition("S1", new ActionInvocation("Denied", new[] { "1" }), "reason"));
        graph.Metadata["key"] = "value";
        var path = Path.Combine(Path.GetTempPath(), $"sek_{System.Guid.NewGuid():N}.seexpl");
        try
        {
            SeexplDocument.SaveGraph(graph, path);
            var loaded = SeexplDocument.Load(path);
            var expected = SeexplDocument.FromGraph(graph);
            expected.GeneratedUtc = loaded.GeneratedUtc;
            Assert.Equal(expected.ToJson(), loaded.ToJson());
            Assert.Equal(expected.ToJson(), File.ReadAllText(path));
            var back = loaded.ToGraph();
            Assert.Equal(graph.States, back.States);
            Assert.Equal(graph.Transitions.Select(transition => transition.Action.Display), back.Transitions.Select(transition => transition.Action.Display));
            Assert.Equal("return", back.Transitions.Last().Action.Kind);
            Assert.Equal("result", back.Transitions.Last().Action.Result);
            Assert.Equal(2, back.NegativeTransitions.Count);
            Assert.Equal("permission.denied", back.NegativeTransitions[0].ObligationId);
            Assert.Equal("System.InvalidOperationException", back.NegativeTransitions[0].ExpectedExceptionType);
            Assert.Equal("guard", back.NegativeTransitions[0].ExpectedMessageContains);
            Assert.Equal("value", back.Metadata["key"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Seexpl_NullDocumentAndLegacyNegativeFieldsRemainDistinct()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sek_{System.Guid.NewGuid():N}.seexpl");
        try
        {
            File.WriteAllText(path, """
                {"machine":"M","negativeTransitions":[{"from":"S0","action":"Denied","arguments":null,"reason":null}]}
                """);
            var negative = Assert.Single(SeexplDocument.Load(path).ToGraph().NegativeTransitions);
            Assert.Empty(negative.Action.Arguments);
            Assert.Equal("", negative.Reason);
            File.WriteAllText(path, "null");
            Assert.Contains("not a valid .seexpl", Assert.Throws<InvalidDataException>(() => SeexplDocument.Load(path)).Message);
            Assert.Throws<System.ArgumentNullException>(() => SeexplDocument.SaveGraph(null!, path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Seexpl_MissingParentDoesNotLeaveAnOwnedTemporaryArtifact()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sek_{System.Guid.NewGuid():N}");
        Assert.Throws<DirectoryNotFoundException>(() => SeexplDocument.SaveGraph(Sample(), Path.Combine(directory, "graph.seexpl")));
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Seexpl_BoundedSave_PreservesArtifactAndCleansOnlyOwnedTemp(bool directlyFromGraph)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sek_{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "graph.seexpl");
        var unrelated = Path.Combine(directory, "other.tmp");
        try
        {
            Assert.Throws<InvalidDataException>(() =>
            {
                if (directlyFromGraph) SeexplDocument.SaveGraph(Sample(), path, 64);
                else SeexplDocument.FromGraph(Sample()).Save(path, 64);
            });
            Assert.Empty(Directory.GetFiles(directory));
            File.WriteAllText(path, "old artifact");
            File.WriteAllText(unrelated, "unrelated");
            var error = Assert.Throws<InvalidDataException>(() =>
            {
                if (directlyFromGraph) SeexplDocument.SaveGraph(Sample(), path, 64);
                else SeexplDocument.FromGraph(Sample()).Save(path, 64);
            });
            Assert.Contains("64 bytes", error.Message);
            Assert.Equal("old artifact", File.ReadAllText(path));
            Assert.Equal("unrelated", File.ReadAllText(unrelated));
            Assert.Equal(2, Directory.GetFiles(directory).Length);
            if (directlyFromGraph) SeexplDocument.SaveGraph(Sample(), path);
            else SeexplDocument.FromGraph(Sample()).Save(path);
            Assert.Equal("M", SeexplDocument.Load(path).Machine);
            Assert.Equal(2, Directory.GetFiles(directory).Length);
            var document = SeexplDocument.Load(path);
            var length = new FileInfo(path).Length;
            document.Save(path, length);
            Assert.Equal(length, new FileInfo(path).Length);
            Assert.Throws<InvalidDataException>(() => document.Save(path, length - 1));
            Assert.Equal(length, new FileInfo(path).Length);
            Assert.Equal(2, Directory.GetFiles(directory).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Seexpl_Load_RejectsOversizedArtifactBeforeParsing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sek_{System.Guid.NewGuid():N}.seexpl");
        try
        {
            using (var stream = File.Create(path)) stream.SetLength(25 * 1024 * 1024 + 1);
            var error = Assert.Throws<InvalidDataException>(() => SeexplDocument.Load(path));
            Assert.Contains("26214400 bytes", error.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Seexpl_SaveGraph_CancellationPreservesArtifact()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sek_{System.Guid.NewGuid():N}.seexpl");
        try
        {
            File.WriteAllText(path, "old artifact");
            using var cancellation = new System.Threading.CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<System.OperationCanceledException>(() => SeexplDocument.SaveGraph(Sample(), path, cancellationToken: cancellation.Token));
            Assert.Equal("old artifact", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
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
