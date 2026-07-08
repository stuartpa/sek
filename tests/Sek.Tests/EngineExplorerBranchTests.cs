using System;
using System.Collections.Generic;
using System.Linq;
using Sek.Engine;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for <see cref="Explorer"/> and <see cref="ModelIntrospector"/>: model-derived
/// negative edges (an action guard-disabled for every argument), exploration bounds
/// (<c>MaxDepth</c>/<c>MaxStates</c>/<c>MaxTransitions</c>), steering goals, event-action kinds,
/// non-void return results, models without accepting conditions, and introspector label/domain
/// fallbacks. These target arms the end-to-end sample explorations do not reach.
/// </summary>
public class EngineExplorerBranchTests
{
    public enum Amt { Small, Big }

    /// <summary>A rule whose single label is enabled for one argument and guard-disabled for
    /// another in the same state — the "enabled-here wins over disabled" negative-edge arm.</summary>
    public sealed class ArgGuardModel : ModelProgram
    {
        public int N { get; set; }

        [Rule("M.Add")]
        public void Add(Amt a)
        {
            Require(N + Cost(a) <= 3, "over cap");
            N += Cost(a);
        }

        private static int Cost(Amt a) => a == Amt.Small ? 1 : 3;

        [AcceptingCondition]
        public bool Full() => N == 3;
    }

    /// <summary>A gate: <c>Use</c> is guard-disabled until <c>Open</c> — a purely-negative edge
    /// in the initial state.</summary>
    public sealed class GateModel : ModelProgram
    {
        public bool Open { get; set; }

        [Rule("G.Open")]
        public void OpenIt()
        {
            Require(!Open, "already open");
            Open = true;
        }

        [Rule("G.Use")]
        public void Use() => Require(Open, "not open");

        [AcceptingCondition]
        public bool Ready() => Open;
    }

    /// <summary>A counter with a non-void action (return-value result) and no argument.</summary>
    public sealed class ResultModel : ModelProgram
    {
        public int N { get; set; }

        [Rule("R.Next")]
        public int Next()
        {
            Require(N < 3, "cap");
            return ++N;
        }

        [AcceptingCondition]
        public bool Done() => N == 3;
    }

    /// <summary>A model with no accepting conditions (no state is accepting).</summary>
    public sealed class NoAcceptModel : ModelProgram
    {
        public int N { get; set; }

        [Rule("N.Inc")]
        public void Inc()
        {
            Require(N < 3, "cap");
            N++;
        }
    }

    /// <summary>A bare <c>[Rule]</c> with no explicit label → introspector derives Type.Method.</summary>
    public sealed class BareLabelModel : ModelProgram
    {
        public int N { get; set; }

        [Rule]
        public void Tick()
        {
            Require(N < 2, "cap");
            N++;
        }
    }

    /// <summary>A rule referencing a domain method that does not exist → resolver throws.</summary>
    public sealed class MissingDomainModel : ModelProgram
    {
        [Rule("D.Do")]
        public void Do([Domain("NoSuchMethod")] int x)
        {
        }
    }

    private static ExplorationResult Explore<T>(ExplorationOptions? options = null) where T : ModelProgram
    {
        var intro = new ModelIntrospector(typeof(T));
        return new Explorer(intro, options ?? new ExplorationOptions()).Explore(typeof(T).Name);
    }

    // ---- Negative edges ---------------------------------------------------------

    [Fact]
    public void ArgDependentGuard_NegativeEdgeOnlyWhereEveryArgIsDisabled()
    {
        var g = Explore<ArgGuardModel>().Graph;
        // "M.Add" fires legally in the lower states (Small keeps it enabled even when Big is
        // guard-disabled — the "enabled-here wins" arm), so it is a real transition …
        Assert.Contains(g.Transitions, t => t.Action.Name == "M.Add");
        // … and only the full state (N=3), where every argument is disabled, emits it as a
        // negative edge.
        var full = g.States.First(s => s.Accepting).Id;
        Assert.Contains(g.NegativeTransitions, n => n.FromStateId == full && n.Action.Name == "M.Add");
        Assert.All(
            g.NegativeTransitions.Where(n => n.Action.Name == "M.Add"),
            n => Assert.Equal(full, n.FromStateId));
    }

    [Fact]
    public void GuardedAction_EmitsNegativeEdge_InStatesWhereAlwaysDisabled()
    {
        var g = Explore<GateModel>().Graph;
        var s0 = g.InitialStateId;
        // Use is illegal in the initial (unopened) state.
        Assert.Contains(g.NegativeTransitions, n => n.FromStateId == s0 && n.Action.Name == "G.Use");
        // Once opened, Use is enabled, so no negative edge for it there.
        var openState = g.States.First(s => !s.Initial).Id;
        Assert.DoesNotContain(g.NegativeTransitions, n => n.FromStateId == openState && n.Action.Name == "G.Use");
    }

    // ---- Bounds -----------------------------------------------------------------

    [Fact]
    public void MaxDepth_LimitsExpansion()
    {
        var deep = Explore<ResultModel>();
        var shallow = Explore<ResultModel>(new ExplorationOptions { MaxDepth = 1 });
        Assert.True(shallow.Graph.States.Count < deep.Graph.States.Count);
    }

    [Fact]
    public void MaxStates_SetsHitBound()
    {
        var r = Explore<ResultModel>(new ExplorationOptions { MaxStates = 2 });
        Assert.True(r.HitBound);
        Assert.True(r.Graph.States.Count <= 2);
    }

    [Fact]
    public void MaxTransitions_SetsHitBound()
    {
        var r = Explore<ResultModel>(new ExplorationOptions { MaxTransitions = 1 });
        Assert.True(r.HitBound);
        Assert.True(r.Graph.Transitions.Count <= 1);
    }

    // ---- Steering goals ---------------------------------------------------------

    [Fact]
    public void GoalPredicate_RecordsGoalStates()
    {
        var r = Explore<ResultModel>(new ExplorationOptions { GoalPredicate = m => ((ResultModel)m).N == 2 });
        Assert.True(r.Graph.Metadata.ContainsKey("goals"));
        Assert.NotEmpty(r.Graph.Metadata["goals"]);
    }

    [Fact]
    public void GoalPredicate_ThatThrows_IsTreatedAsNotAGoal()
    {
        var r = Explore<ResultModel>(new ExplorationOptions { GoalPredicate = _ => throw new InvalidOperationException() });
        // Exploration still completes; the goals list is empty (no state matched).
        Assert.True(r.Graph.Metadata.ContainsKey("goals"));
        Assert.Equal(string.Empty, r.Graph.Metadata["goals"]);
    }

    // ---- Action kinds / results -------------------------------------------------

    [Fact]
    public void EventActionLabels_MarkTransitionsAsEvents()
    {
        var r = Explore<GateModel>(new ExplorationOptions { EventActionLabels = new HashSet<string> { "Use" } });
        Assert.Contains(r.Graph.Transitions, t => t.Action.Name == "G.Use" && t.Action.Kind == "event");
        Assert.Contains(r.Graph.Transitions, t => t.Action.Name == "G.Open" && t.Action.Kind == "call");
    }

    [Fact]
    public void NonVoidRule_RecordsReturnResult()
    {
        var r = Explore<ResultModel>();
        Assert.Contains(r.Graph.Transitions, t => t.Action.Name == "R.Next" && t.Action.Result is not null);
    }

    // ---- Accepting conditions ---------------------------------------------------

    [Fact]
    public void ModelWithoutAcceptingConditions_HasNoAcceptingStates()
    {
        var g = Explore<NoAcceptModel>().Graph;
        Assert.DoesNotContain(g.States, s => s.Accepting);
    }

    // ---- Introspector fallbacks -------------------------------------------------

    [Fact]
    public void BareRule_DerivesLabelFromTypeAndMethod()
    {
        var intro = new ModelIntrospector(typeof(BareLabelModel));
        Assert.Equal("BareLabelModel.Tick", intro.Rules.Single().ActionLabel);
    }

    [Fact]
    public void MissingDomainMethod_ThrowsDuringExploration()
    {
        Assert.Throws<InvalidOperationException>(() => Explore<MissingDomainModel>());
    }
}
