using System.Collections.Generic;
using System.Linq;
using Sek.Cord.Ast;
using Sek.Engine;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for the remaining node-kind arms of <see cref="BehaviorExplorer"/>'s recursive
/// walks (CollectSymbols / CollectReturnBindings / ContainsFail / Build): choice, parallel, let,
/// argument-pinned invocations, the <c>_</c> wildcard target, and negation.
/// </summary>
public class EngineCompileCoverageTests
{
    private static InvocationBehavior Inv(string target, params string[] args) =>
        new() { Target = target, Args = args.Length == 0 ? null : args.ToList() };

    private static BehaviorExplorer Explorer() => new(_ => null, new[] { "A", "B", "C" });

    [Fact]
    public void Compile_Choice_Parallel_Let_VisitAllArms()
    {
        // Choice and Parallel arms in the collectors (Build treats parallel as empty, but the
        // symbol/binding/fail walks still traverse it).
        Assert.NotNull(Explorer().Compile(new ChoiceBehavior { Items = { Inv("A"), Inv("B") } }));
        Assert.NotNull(Explorer().Compile(new ParallelBehavior { Items = { Inv("A"), Inv("B") } }));

        // Let arm.
        var letB = new LetBehavior { Inner = Inv("A") };
        letB.Vars.Add(new Parameter { Type = "int", Name = "x" });
        Assert.NotNull(Explorer().Compile(letB));

        // LooseSequence and Permutation arms (in the binding/fail collectors).
        Assert.NotNull(Explorer().Compile(new LooseSequenceBehavior { Items = { Inv("A"), Inv("B") } }));
        Assert.NotNull(Explorer().Compile(new PermutationBehavior { Items = { Inv("A"), Inv("B") } }));
        // Fail nested inside a choice/parallel so the collectors recurse those arms too.
        Assert.True(Explorer().Compile(new ChoiceBehavior { Items = { new FailBehavior { Inner = Inv("A") }, Inv("B") } }).HasFailStates);
        Assert.True(Explorer().Compile(new ParallelBehavior { Items = { new FailBehavior { Inner = Inv("A") }, Inv("B") } }).HasFailStates);
    }

    [Fact]
    public void Explore_PinnedArgs_Wildcard_Negation()
    {
        // Argument-pinned invocation → CollectSymbols yields "A(1)"; Build makes an atom for it.
        var pinned = new SequenceBehavior { Items = { Inv("A", "1"), Inv("B", "2") } };
        var g1 = Explorer().Explore("Pinned", pinned);
        Assert.NotEmpty(g1.Transitions);

        // Wildcard target `_` and a negated invocation are handled specially (no symbol collected).
        var special = new SequenceBehavior { Items = { Inv("_"), new InvocationBehavior { Target = "C", Negated = true } } };
        var g2 = Explorer().Explore("Special", special);
        Assert.NotNull(g2);
    }

    [Fact]
    public void Explore_RepetitionBounds_And_Permutation()
    {
        // Repetition with explicit {min..max} bounds and a 2-item permutation.
        var body = new SequenceBehavior
        {
            Items =
            {
                new RepetitionBehavior { Inner = Inv("A"), Op = "{}", Min = 1, Max = 2 },
                new PermutationBehavior { Items = { Inv("B"), Inv("C") } },
            },
        };
        var g = Explorer().Explore("RepPerm", body);
        Assert.NotEmpty(g.Transitions);
    }
}
