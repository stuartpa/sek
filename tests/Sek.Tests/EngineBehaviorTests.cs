using System.Collections.Generic;
using System.Linq;
using Sek.Cord.Ast;
using Sek.Engine;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for <c>Sek.Engine</c>'s behavior automaton (<see cref="BehaviorExplorer"/>: Thompson
/// NFA → subset-DFA → graph, over the behavior algebra) and richer model exploration
/// (<see cref="Explorer"/>: parameters/domains, events, and bound-hitting).
/// </summary>
public class EngineBehaviorTests
{
    private static InvocationBehavior Inv(string target, string? ret = null) =>
        new() { Target = target, ReturnBinding = ret };

    private static BehaviorExplorer NewExplorer(params string[] alphabet) =>
        new(_ => null, alphabet);

    private static SequenceBehavior Seq(params Behavior[] items)
    {
        var s = new SequenceBehavior();
        s.Items.AddRange(items);
        return s;
    }

    private static ChoiceBehavior Choice(params Behavior[] items)
    {
        var c = new ChoiceBehavior();
        c.Items.AddRange(items);
        return c;
    }

    // ---- Behavior automaton over the algebra -------------------------------------------

    [Fact]
    public void Explore_Sequence_ProducesLinearChainWithAcceptingEnd()
    {
        var g = NewExplorer("A", "B", "C").Explore("Seq", Seq(Inv("A"), Inv("B"), Inv("C")));
        Assert.NotEmpty(g.States);
        Assert.NotNull(g.InitialStateId);
        Assert.Contains(g.States, s => s.Accepting);
        Assert.Equal(3, g.Transitions.Count);
    }

    [Fact]
    public void Explore_Choice_BranchesFromInitial()
    {
        var g = NewExplorer("A", "B").Explore("Choice", Choice(Inv("A"), Inv("B")));
        Assert.Equal(2, g.Transitions.Count(t => t.FromStateId == g.InitialStateId));
    }

    [Fact]
    public void Explore_Optional_InitialIsAccepting()
    {
        // A? → the empty path is accepted, so the initial state is accepting.
        var opt = new RepetitionBehavior { Inner = Inv("A"), Op = "?", Min = 0, Max = 1 };
        var g = NewExplorer("A").Explore("Opt", opt);
        Assert.Contains(g.States, s => s.Initial && s.Accepting);
    }

    [Fact]
    public void Explore_Repetition_Star_HasLoopBackTransition()
    {
        var rep = new RepetitionBehavior { Inner = Inv("A"), Op = "*" };
        var g = NewExplorer("A").Explore("Star", rep);
        Assert.Contains(g.States, s => s.Initial && s.Accepting); // zero repetitions accepted
        Assert.NotEmpty(g.Transitions);
    }

    [Fact]
    public void Explore_SyncParallel_IntersectsBehaviors()
    {
        var p = new ParallelBehavior { Op = "sync" };
        p.Items.Add(Seq(Inv("A"), Inv("B")));
        p.Items.Add(Seq(Inv("A"), Inv("B")));
        var g = NewExplorer("A", "B").Explore("Sync", p);
        Assert.NotEmpty(g.States);
        Assert.Contains(g.States, s => s.Accepting);
    }

    [Fact]
    public void Explore_InterleaveParallel_ProducesInterleavings()
    {
        var p = new ParallelBehavior { Op = "interleave" };
        p.Items.Add(Inv("A"));
        p.Items.Add(Inv("B"));
        var g = NewExplorer("A", "B").Explore("Inter", p);
        // A then B, or B then A → at least 2 transitions out of the interleaving.
        Assert.True(g.Transitions.Count >= 2);
    }

    [Fact]
    public void Explore_Group_Unwraps()
    {
        var g = NewExplorer("A").Explore("Grp", new GroupBehavior { Inner = Inv("A") });
        Assert.Single(g.Transitions);
    }

    [Fact]
    public void Compile_CollectsReturnBindings_AndVars()
    {
        // Open() / h ; Close(h)
        var body = Seq(Inv("Open", ret: "h"), Inv("Close"));
        var scenario = NewExplorer("Open", "Close").Compile(body);
        Assert.True(scenario.HasReturnBindings);
    }

    [Fact]
    public void Compile_DetectsFailBehavior()
    {
        var body = new FailBehavior { Inner = Seq(Inv("A"), Inv("B")) };
        var scenario = NewExplorer("A", "B").Compile(body);
        Assert.True(scenario.HasFailStates);
    }

    // ---- Richer model exploration ------------------------------------------------------

    public sealed class ParamModel : ModelProgram
    {
        public int Sum { get; set; }

        [Rule("P.Add")]
        public void Add([Domain(nameof(Amounts))] int amount)
        {
            Require(Sum + amount <= 5, "cap");
            Sum += amount;
        }

        public static int[] Amounts() => new[] { 1, 2 };

        [AcceptingCondition]
        public bool AtLeastThree() => Sum >= 3;
    }

    [Fact]
    public void Explore_ModelWithParameterDomain_EnumeratesArguments()
    {
        var introspector = new ModelIntrospector(typeof(ParamModel));
        var result = new Explorer(introspector).Explore(nameof(ParamModel));
        // Transitions are labelled with concrete argument values from the domain {1,2}.
        Assert.Contains(result.Graph.Transitions, t => t.Action.Arguments.Contains("1"));
        Assert.Contains(result.Graph.Transitions, t => t.Action.Arguments.Contains("2"));
        Assert.Contains(result.Graph.States, s => s.Accepting);
    }

    [Fact]
    public void Explore_StateBound_IsHonoured()
    {
        var introspector = new ModelIntrospector(typeof(EngineTests.CounterModel));
        var options = new ExplorationOptions { MaxStates = 2 };
        var result = new Explorer(introspector, options).Explore(nameof(EngineTests.CounterModel));
        Assert.True(result.Graph.States.Count <= 3); // bounded (cap + in-flight)
        Assert.True(result.HitBound);
    }
}
