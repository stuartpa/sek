using Sek.Core.Analysis;
using Sek.Core.Model;
using Sek.Engine;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Item 2 — point-shoot / accept-completion steering. Covers the pure goal-reaching graph
/// prune (<see cref="GraphAnalysis.FilterToReaching"/>) and the engine's goal-state tracking
/// via <see cref="ExplorationOptions.GoalPredicate"/>.
/// </summary>
public class SteeringTests
{
    private static ExplorationGraph LinePlusDeadEnd()
    {
        // S0 -> S1 -> S2(goal) ; S0 -> S3(dead end, not a goal, cannot reach goal)
        var g = new ExplorationGraph { InitialStateId = "S0" };
        g.States.Add(new ModelState("S0", "h0", Initial: true));
        g.States.Add(new ModelState("S1", "h1"));
        g.States.Add(new ModelState("S2", "h2", Accepting: true));
        g.States.Add(new ModelState("S3", "h3"));
        var a = new ActionInvocation("go", System.Array.Empty<string>());
        g.Transitions.Add(new Transition("S0", a, "S1"));
        g.Transitions.Add(new Transition("S1", a, "S2"));
        g.Transitions.Add(new Transition("S0", a, "S3"));
        return g;
    }

    [Fact]
    public void FilterToReaching_KeepsOnlyGoalReachingPaths()
    {
        var g = LinePlusDeadEnd();
        var count = GraphAnalysis.FilterToReaching(g, s => s.Id == "S2");

        Assert.Equal(1, count);
        Assert.Equal(new[] { "S0", "S1", "S2" }, g.States.Select(s => s.Id).OrderBy(x => x));
        Assert.DoesNotContain(g.States, s => s.Id == "S3"); // dead end pruned
        Assert.All(g.Transitions, t => Assert.NotEqual("S3", t.ToStateId));
    }

    [Fact]
    public void FilterToReaching_NoGoals_KeepsOnlyInitial()
    {
        var g = LinePlusDeadEnd();
        var count = GraphAnalysis.FilterToReaching(g, _ => false);

        Assert.Equal(0, count);
        Assert.Single(g.States);
        Assert.Equal("S0", g.States[0].Id);
        Assert.Empty(g.Transitions);
    }

    [Fact]
    public void FilterToReaching_LoopBackToInitial_IsHandled()
    {
        var g = new ExplorationGraph { InitialStateId = "S0" };
        g.States.Add(new ModelState("S0", "h0", Initial: true));
        g.States.Add(new ModelState("S1", "h1"));
        var a = new ActionInvocation("t", System.Array.Empty<string>());
        g.Transitions.Add(new Transition("S0", a, "S1"));
        g.Transitions.Add(new Transition("S1", a, "S0"));
        var count = GraphAnalysis.FilterToReaching(g, s => s.Id == "S1");
        Assert.Equal(1, count);
        Assert.Equal(2, g.States.Count); // both on the cycle reaching S1
    }

    [Fact]
    public void Explorer_RecordsGoalStates_ViaGoalPredicate()
    {
        var introspector = new ModelIntrospector(typeof(Counter));
        var opts = new ExplorationOptions { MaxStates = 100, MaxDepth = 100, MaxTransitions = 500 };
        opts.GoalPredicate = o => o is Counter c && c.N == 3; // steer to N == 3

        var result = new Explorer(introspector, opts).Explore("Counter");

        Assert.True(result.Graph.Metadata.ContainsKey("goals"));
        var goals = result.Graph.Metadata["goals"].Split(',', System.StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(goals); // exactly one state has N == 3

        // After steering to the goal, only the states on the path to N==3 remain.
        GraphAnalysis.FilterToReaching(result.Graph, s => goals.Contains(s.Id));
        Assert.Equal(4, result.Graph.States.Count); // N = 0,1,2,3
    }

    [Fact]
    public void Explorer_NoGoalPredicate_EmitsNoGoalsMetadata()
    {
        var introspector = new ModelIntrospector(typeof(Counter));
        var result = new Explorer(introspector, new ExplorationOptions { MaxDepth = 100 }).Explore("Counter");
        Assert.False(result.Graph.Metadata.ContainsKey("goals"));
    }

    // ---- phased point-shoot building blocks ----

    [Fact]
    public void Explorer_RetainsPerStateJson()
    {
        var introspector = new ModelIntrospector(typeof(Counter));
        var result = new Explorer(introspector, new ExplorationOptions { MaxDepth = 100 }).Explore("Counter");
        // Every graph state has a serialized snapshot, and the initial one reflects N == 0.
        Assert.Equal(result.Graph.States.Count, result.StateJson.Count);
        Assert.Contains("\"N\":0", result.StateJson[result.Graph.InitialStateId!].Replace(" ", ""));
    }

    [Fact]
    public void Explorer_ResumesFromStartJson()
    {
        var introspector = new ModelIntrospector(typeof(Counter));
        var full = new Explorer(introspector, new ExplorationOptions { MaxDepth = 100 }).Explore("Counter");
        // Resume from the N == 2 state; only N = 2, 3 remain reachable.
        var n2 = full.StateJson.Values.First(j => j.Replace(" ", "").Contains("\"N\":2"));
        var resumed = new Explorer(introspector, new ExplorationOptions { MaxDepth = 100 }).Explore("Counter", n2);
        Assert.Equal(2, resumed.Graph.States.Count); // N = 2 (start) and N = 3
    }

    [Fact]
    public void MergeByHash_UnifiesStatesAcrossGraphs()
    {
        // g1: A(hX) -> B(hY).  g2 resumes at B(hY) -> C(hZ).  Merged: A -> B -> C (3 states).
        var g1 = new ExplorationGraph { InitialStateId = "S0" };
        g1.States.Add(new ModelState("S0", "hX", Initial: true));
        g1.States.Add(new ModelState("S1", "hY"));
        g1.Transitions.Add(new Transition("S0", new ActionInvocation("a", System.Array.Empty<string>()), "S1"));

        var g2 = new ExplorationGraph { InitialStateId = "S0" };
        g2.States.Add(new ModelState("S0", "hY", Initial: true));
        g2.States.Add(new ModelState("S1", "hZ", Accepting: true));
        g2.Transitions.Add(new Transition("S0", new ActionInvocation("b", System.Array.Empty<string>()), "S1"));

        var merged = GraphAnalysis.MergeByHash("M", new[] { g1, g2 }, "hX");
        Assert.Equal(3, merged.States.Count);        // hX, hY, hZ unified (hY shared)
        Assert.Equal(2, merged.Transitions.Count);   // a, b
        Assert.Equal("hX", merged.FindState(merged.InitialStateId!)!.Hash);
        Assert.Single(merged.States, s => s.Accepting);
    }

    [Fact]
    public void FilterToGoalThenAccepting_KeepsRootGoalAccepting()
    {
        // root(hR) -> goal(hG) -> accept(hA) ; plus a dead branch root -> D(hD) that reaches no goal.
        var g = new ExplorationGraph { InitialStateId = "S0" };
        g.States.Add(new ModelState("S0", "hR", Initial: true));
        g.States.Add(new ModelState("S1", "hG"));            // goal
        g.States.Add(new ModelState("S2", "hA", Accepting: true));
        g.States.Add(new ModelState("S3", "hD"));            // dead end
        var a = new ActionInvocation("x", System.Array.Empty<string>());
        g.Transitions.Add(new Transition("S0", a, "S1"));
        g.Transitions.Add(new Transition("S1", a, "S2"));
        g.Transitions.Add(new Transition("S0", a, "S3"));

        GraphAnalysis.FilterToGoalThenAccepting(g, new HashSet<string> { "hG" });

        Assert.Equal(new[] { "hA", "hG", "hR" }, g.States.Select(s => s.Hash).OrderBy(x => x)); // dead end pruned
        Assert.DoesNotContain(g.States, s => s.Hash == "hD");
    }

    /// <summary>A minimal model: counts 0..3 then stops. Used to exercise goal tracking.</summary>
    public sealed class Counter : ModelProgram
    {
        public int N { get; set; }

        [Rule("Inc")]
        public void Inc()
        {
            Require(N < 3, "at max");
            N++;
        }
    }
}
