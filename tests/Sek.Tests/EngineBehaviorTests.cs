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

    [Theory]
    [InlineData("group")]
    [InlineData("preconstraint")]
    [InlineData("let")]
    [InlineData("bind")]
    [InlineData("permutation")]
    [InlineData("loose")]
    [InlineData("parallel")]
    public void Compile_WrappersPreserveProducerReturnBinding(string kind)
    {
        var producer = new InvocationBehavior { Target = "Produce", Args = new() { "1" }, ReturnBinding = "handle" };
        Behavior body = kind switch
        {
            "group" => new GroupBehavior { Inner = producer },
            "preconstraint" => new PreconstraintBehavior { Inner = producer },
            "let" => new LetBehavior { Inner = producer },
            "bind" => new BindBehavior { Inner = producer },
            "permutation" => new PermutationBehavior { Items = { producer } },
            "loose" => new LooseSequenceBehavior { Items = { producer } },
            _ => new ParallelBehavior { Op = "interleave", Items = { producer } },
        };
        var scenario = NewExplorer("Produce").Compile(body);
        Assert.True(scenario.HasReturnBindings);
        Assert.True(scenario.TryStep(scenario.Start, "Produce(1)", out var end));
        Assert.True(scenario.IsAccepting(end));
        Assert.False(scenario.HasFailStates);
    }

    [Theory]
    [InlineData("sequence")]
    [InlineData("choice")]
    [InlineData("permutation")]
    [InlineData("loose")]
    [InlineData("parallel")]
    [InlineData("repeat")]
    public void Compile_EmptyFormsPreserveTheirLanguageWithoutInventingActions(string kind)
    {
        Behavior body = kind switch
        {
            "sequence" => new SequenceBehavior(),
            "choice" => new ChoiceBehavior(),
            "permutation" => new PermutationBehavior(),
            "loose" => new LooseSequenceBehavior(),
            "parallel" => new ParallelBehavior { Op = "interleave" },
            _ => new RepetitionBehavior { Inner = Inv("A"), Op = "{}", Min = 0, Max = 0 },
        };
        var scenario = NewExplorer("A").Compile(body);
        Assert.Equal(kind != "choice", scenario.IsAccepting(scenario.Start));
        Assert.False(scenario.TryStep(scenario.Start, "A", out _));
        Assert.False(scenario.HasReturnBindings);
        Assert.False(scenario.HasFailStates);
    }

    [Fact]
    public void Compile_OptionsConstructor_UsesSharedCancellationBudget()
    {
        using var cancellation = new System.Threading.CancellationTokenSource();
        var options = new ExplorationOptions { CancellationToken = cancellation.Token };
        var budget = options.Budget = new ExplorationBudget(options);
        var explorer = new BehaviorExplorer(_ => null, new[] { "A" }, options);
        cancellation.Cancel();
        var error = Assert.Throws<System.InvalidOperationException>(() => explorer.Compile(Inv("A")));
        Assert.Contains("INCOMPLETE", error.Message);
        Assert.Equal("cancelled", budget.TerminationReason);
        Assert.Equal(0, budget.States);
        Assert.Equal(0, budget.Transitions);
    }

    [Fact]
    public void Compile_HugeRepetition_StopsBeforeBuildingUnboundedNfa()
    {
        var options = new ExplorationOptions { MaxCandidateEvaluations = 100 };
        var explorer = new BehaviorExplorer(_ => null, new[] { "A" }, options);
        var body = new RepetitionBehavior { Inner = Inv("A"), Op = "{}", Min = int.MaxValue, Max = int.MaxValue };
        var error = Assert.Throws<System.InvalidOperationException>(() => explorer.Compile(body));
        Assert.Contains("INCOMPLETE", error.Message);
        Assert.Equal("candidate-limit", options.Budget!.TerminationReason);
        Assert.Equal(100, options.Budget.CandidateEvaluations);
        Assert.Equal(0, options.Budget.States);
        Assert.Equal(0, options.Budget.Transitions);
    }

    [Fact]
    public void Compile_RecursiveReference_ThrowsInsteadOfOverflowingStack()
    {
        var recursive = Inv("Self");
        var options = new ExplorationOptions();
        var explorer = new BehaviorExplorer(name => name == "Self" ? recursive : null, new[] { "A" }, options);
        var error = Assert.Throws<System.InvalidOperationException>(() => explorer.Compile(recursive));
        Assert.Contains("INCOMPLETE", error.Message);
        Assert.Equal("behavior-recursion-limit", options.Budget!.TerminationReason);
    }

    [Fact]
    public void Compile_SmallGraphLimits_DoNotChargeNfaNodesAsModelStates()
    {
        var options = new ExplorationOptions { MaxStates = 1, MaxTransitions = 1, MaxDepth = 0 };
        var scenario = new BehaviorExplorer(_ => null, new[] { "A", "B" }, options)
            .Compile(Seq(Inv("A"), Inv("B")));
        Assert.True(scenario.TryStep(scenario.Start, "A", out var next));
        Assert.True(scenario.TryStep(next, "B", out var end));
        Assert.True(scenario.IsAccepting(end));
        Assert.Equal(0, options.Budget!.States);
        Assert.Equal(0, options.Budget.Transitions);
    }

    [Theory]
    [InlineData("sync")]
    [InlineData("interleave")]
    [InlineData("syncinterleave")]
    public void Compile_LazyProduct_UsesSharedBudgetAfterCompilation(string op)
    {
        var body = new ParallelBehavior { Op = op, Items = { Inv("A"), Inv("A") } };
        var options = new ExplorationOptions();
        var scenario = new BehaviorExplorer(_ => null, new[] { "A" }, options).Compile(body);
        options.MaxCandidateEvaluations = options.Budget!.CandidateEvaluations + 1;
        var error = Assert.Throws<System.InvalidOperationException>(() => scenario.TryStep(scenario.Start, "A", out _));
        Assert.Contains("INCOMPLETE", error.Message);
        Assert.Equal("candidate-limit", options.Budget.TerminationReason);
    }

    [Fact]
    public void Compile_CachedQueries_StillCheckCancellation()
    {
        using var cancellation = new System.Threading.CancellationTokenSource();
        var options = new ExplorationOptions { CancellationToken = cancellation.Token };
        var scenario = new BehaviorExplorer(_ => null, new[] { "A" }, options).Compile(Inv("A"));
        Assert.True(scenario.TryStep(scenario.Start, "A", out _));
        cancellation.Cancel();
        Assert.Throws<System.InvalidOperationException>(() => scenario.TryStep(scenario.Start, "A", out _));
        Assert.Equal("cancelled", options.Budget!.TerminationReason);
    }

    [Theory]
    [InlineData(0, 10, "state-limit", 0, 0)]
    [InlineData(1, 10, "state-limit", 1, 0)]
    [InlineData(10, 0, "transition-limit", 1, 0)]
    [InlineData(10, 1, "transition-limit", 2, 1)]
    public void Explore_CountLimits_EmitConsistentIncompleteGraph(int states, int transitions, string reason, int expectedStates, int expectedTransitions)
    {
        var options = new ExplorationOptions { MaxStates = states, MaxTransitions = transitions };
        var graph = new BehaviorExplorer(_ => null, new[] { "A", "B" }, options)
            .Explore("Bounded", Seq(Inv("A"), Inv("B")));
        Assert.Equal(expectedStates, graph.States.Count);
        Assert.Equal(expectedTransitions, graph.Transitions.Count);
        Assert.Equal("INCOMPLETE", graph.Metadata["completeness"]);
        Assert.Equal(reason, graph.Metadata["terminationReason"]);
        Assert.Equal(graph.States.Count, options.Budget!.States);
        Assert.Equal(graph.Transitions.Count, options.Budget.Transitions);
        Assert.Equal(options.Budget.CandidateEvaluations.ToString(), graph.Metadata["candidateEvaluations"]);
        Assert.All(graph.Transitions, edge =>
        {
            Assert.Contains(graph.States, state => state.Id == edge.FromStateId);
            Assert.Contains(graph.States, state => state.Id == edge.ToStateId);
        });
    }

    [Theory]
    [InlineData(0, "INCOMPLETE", "depth-limit", 0)]
    [InlineData(1, "INCOMPLETE", "depth-limit", 1)]
    [InlineData(2, "COMPLETE", "complete", 2)]
    public void Explore_DepthLimit_DistinguishesTerminalBoundaryFromOmittedSuccessor(int depth, string completeness, string reason, int transitions)
    {
        var options = new ExplorationOptions { MaxDepth = depth };
        var graph = new BehaviorExplorer(_ => null, new[] { "A", "B" }, options)
            .Explore("Depth", Seq(Inv("A"), Inv("B")));
        Assert.Equal(completeness, graph.Metadata["completeness"]);
        Assert.Equal(reason, graph.Metadata["terminationReason"]);
        Assert.Equal(transitions, graph.Transitions.Count);
    }

    [Fact]
    public void Explore_ExactCountBoundsAndExistingCycle_AreComplete()
    {
        var options = new ExplorationOptions { MaxStates = 2, MaxTransitions = 2, MaxDepth = 1 };
        var graph = new BehaviorExplorer(_ => null, new[] { "A" }, options)
            .Explore("Loop", new RepetitionBehavior { Inner = Inv("A"), Op = "*" });
        Assert.Equal(2, graph.States.Count);
        Assert.Equal(2, graph.Transitions.Count);
        Assert.Equal("COMPLETE", graph.Metadata["completeness"]);
    }

    private sealed class BehaviorClock : System.TimeProvider
    {
        public long Timestamp { get; set; }
        public override long TimestampFrequency => global::System.TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Timestamp;
    }

    [Fact]
    public void Compile_DeadlineDuringSymbolWalk_ThrowsIncomplete()
    {
        var clock = new BehaviorClock();
        var options = new ExplorationOptions { TimeProvider = clock, MaxElapsedTime = System.TimeSpan.FromSeconds(1) };
        var explorer = new BehaviorExplorer(_ =>
        {
            clock.Timestamp = System.TimeSpan.FromSeconds(2).Ticks;
            return null;
        }, new[] { "A" }, options);
        var error = Assert.Throws<System.InvalidOperationException>(() => explorer.Compile(Inv("A")));
        Assert.Contains("INCOMPLETE", error.Message);
        Assert.Equal("deadline", options.Budget!.TerminationReason);
    }

    [Fact]
    public void Compile_CachedQueries_StillCheckDeadline()
    {
        var clock = new BehaviorClock();
        var options = new ExplorationOptions { TimeProvider = clock, MaxElapsedTime = System.TimeSpan.FromSeconds(1) };
        var scenario = new BehaviorExplorer(_ => null, new[] { "A" }, options).Compile(Inv("A"));
        Assert.True(scenario.TryStep(scenario.Start, "A", out _));
        clock.Timestamp = System.TimeSpan.FromSeconds(1).Ticks;
        Assert.Throws<System.InvalidOperationException>(() => scenario.IsAccepting(scenario.Start));
        Assert.Equal("deadline", options.Budget!.TerminationReason);
    }

    [Fact]
    public void Compile_DeepSyntaxWalk_ThrowsInsteadOfOverflowingStack()
    {
        Behavior body = Inv("A");
        for (var depth = 0; depth < 10000; depth++) body = new GroupBehavior { Inner = body };
        var options = new ExplorationOptions();
        var explorer = new BehaviorExplorer(_ => null, new[] { "A" }, options);
        Assert.Throws<System.InvalidOperationException>(() => explorer.Compile(body));
        Assert.Equal("behavior-recursion-limit", options.Budget!.TerminationReason);
    }

    [Fact]
    public void Constructor_RepeatedAlphabetEnumeration_IsCandidateBounded()
    {
        static IEnumerable<string> Alphabet()
        {
            while (true) yield return "A";
        }

        var options = new ExplorationOptions { MaxCandidateEvaluations = 20 };
        Assert.Throws<System.InvalidOperationException>(() => new BehaviorExplorer(_ => null, Alphabet(), options));
        Assert.Equal("candidate-limit", options.Budget!.TerminationReason);
        Assert.Equal(20, options.Budget.CandidateEvaluations);
    }

    [Fact]
    public void Compile_LazySubset_StopsWhileComputingOutgoingTransitions()
    {
        var options = new ExplorationOptions();
        var body = Choice(Enumerable.Range(0, 200).Select(index => (Behavior)Inv("A" + index)).ToArray());
        var scenario = new BehaviorExplorer(_ => null, System.Array.Empty<string>(), options).Compile(body);
        options.MaxCandidateEvaluations = options.Budget!.CandidateEvaluations + 100;
        Assert.Throws<System.InvalidOperationException>(() => scenario.TryStep(scenario.Start, "A0", out _));
        Assert.Equal("candidate-limit", options.Budget.TerminationReason);
        Assert.Equal(0, options.Budget.States);
        Assert.Equal(0, options.Budget.Transitions);
    }

    [Fact]
    public void Compile_SyncInterleave_FullSignatureWalkIsBounded()
    {
        var side = Seq(Enumerable.Range(0, 20).Select(index => (Behavior)Choice(Inv("A" + index), Inv("B" + index))).ToArray());
        var body = new ParallelBehavior { Op = "sync", Items = { side, side } };
        var baseline = new ExplorationOptions();
        new BehaviorExplorer(_ => null, System.Array.Empty<string>(), baseline).Compile(body);
        body.Op = "syncinterleave";
        var options = new ExplorationOptions { MaxCandidateEvaluations = baseline.Budget!.CandidateEvaluations + 100 };
        var explorer = new BehaviorExplorer(_ => null, System.Array.Empty<string>(), options);
        Assert.Throws<System.InvalidOperationException>(() => explorer.Compile(body));
        Assert.Equal("candidate-limit", options.Budget!.TerminationReason);
        Assert.Equal(options.MaxCandidateEvaluations, options.Budget.CandidateEvaluations);
    }

    [Fact]
    public void Explore_LazyExpansionCandidateLimit_ReturnsPartialGraphMetadata()
    {
        var body = Choice(Enumerable.Range(0, 1000).Select(index => (Behavior)Inv("A" + index)).ToArray());
        var baseline = new ExplorationOptions();
        new BehaviorExplorer(_ => null, System.Array.Empty<string>(), baseline).Compile(body);
        var options = new ExplorationOptions { MaxCandidateEvaluations = baseline.Budget!.CandidateEvaluations + 100 };
        var graph = new BehaviorExplorer(_ => null, System.Array.Empty<string>(), options).Explore("Candidates", body);
        Assert.Single(graph.States);
        Assert.Empty(graph.Transitions);
        Assert.Equal("INCOMPLETE", graph.Metadata["completeness"]);
        Assert.Equal("candidate-limit", graph.Metadata["terminationReason"]);
        Assert.Equal(options.MaxCandidateEvaluations.ToString(), graph.Metadata["candidateEvaluations"]);
        Assert.NotNull(graph.InitialStateId);
    }

    [Fact]
    public void Explore_TerminalAtZeroDepthAndZeroEdges_IsComplete()
    {
        var options = new ExplorationOptions { MaxDepth = 0, MaxStates = 1, MaxTransitions = 0 };
        var graph = new BehaviorExplorer(_ => null, System.Array.Empty<string>(), options).Explore("Empty", Seq());
        Assert.Single(graph.States);
        Assert.Empty(graph.Transitions);
        Assert.Equal("COMPLETE", graph.Metadata["completeness"]);
    }

    [Fact]
    public void Compile_CachedQueries_DoNotRepeatedlyChargeCompilerCandidates()
    {
        var options = new ExplorationOptions();
        var scenario = new BehaviorExplorer(_ => null, new[] { "A" }, options).Compile(Inv("A"));
        Assert.True(scenario.TryStep(scenario.Start, "A", out var end));
        var candidates = options.Budget!.CandidateEvaluations;
        for (var query = 0; query < 1000; query++)
        {
            Assert.True(scenario.TryStep(scenario.Start, "A", out _));
            Assert.True(scenario.Permits(scenario.Start, "A"));
            Assert.True(scenario.IsAccepting(end));
            Assert.False(scenario.IsFail(end));
        }

        Assert.Equal(candidates, options.Budget.CandidateEvaluations);
    }

    [Fact]
    public void Explore_WideChoice_DoesNotChargeNonmatchingEdgeCrossProduct()
    {
        var options = new ExplorationOptions();
        var body = Choice(Enumerable.Range(0, 1000).Select(index => (Behavior)Inv("A" + index)).ToArray());
        var graph = new BehaviorExplorer(_ => null, System.Array.Empty<string>(), options).Explore("Wide", body);
        Assert.Equal(1001, graph.States.Count);
        Assert.Equal(1000, graph.Transitions.Count);
        Assert.Equal("COMPLETE", graph.Metadata["completeness"]);
        Assert.True(options.Budget!.CandidateEvaluations < options.MaxCandidateEvaluations);
    }

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
