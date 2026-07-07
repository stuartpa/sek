using System.Collections.Generic;
using System.Linq;
using Sek.Cord.Ast;
using Sek.Engine;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for <see cref="BehaviorExplorer"/>'s recursive walks (CollectSymbols,
/// CollectReturnBindings, ContainsFail, ToDfa) over the rarer behaviour node kinds that the sample
/// scenarios do not use: permutation, loose-sequence, fail, group, preconstraint, bind, repetition,
/// return-bindings, and machine references.
/// </summary>
public class EngineBehaviorCoverageTests
{
    private static InvocationBehavior Inv(string target) => new() { Target = target };

    private static BehaviorExplorer Explorer(Dictionary<string, Behavior>? machines = null) =>
        new(name => machines is not null && machines.TryGetValue(name, out var b) ? b : null,
            new[] { "A", "B", "C" });

    [Fact]
    public void Explore_Permutation_And_LooseSequence()
    {
        var perm = new PermutationBehavior { Items = { Inv("A"), Inv("B") } };
        var g1 = Explorer().Explore("Perm", perm);
        Assert.NotEmpty(g1.Transitions);

        var loose = new LooseSequenceBehavior { Items = { Inv("A"), Inv("B") } };
        var g2 = Explorer().Explore("Loose", loose);
        Assert.NotEmpty(g2.Transitions);
    }

    [Fact]
    public void Explore_Repetition_Group_Preconstraint()
    {
        var body = new SequenceBehavior
        {
            Items =
            {
                new RepetitionBehavior { Inner = Inv("A"), Op = "*" },
                new GroupBehavior { Inner = Inv("B") },
                new PreconstraintBehavior { Code = "true", Inner = Inv("C") },
            },
        };
        var g = Explorer().Explore("RGP", body);
        Assert.NotEmpty(g.Transitions);
    }

    [Fact]
    public void Compile_Fail_MarksModelChecking()
    {
        var fail = new FailBehavior { Inner = Inv("A") };
        var compiled = Explorer().Compile(fail);
        Assert.True(compiled.HasFailStates);
    }

    [Fact]
    public void Compile_ReturnBinding_And_Bind()
    {
        // `A / v ; B` — A produces a value bound to v, referenced by a later action.
        var producer = new InvocationBehavior { Target = "A", ReturnBinding = "v" };
        var body = new BindBehavior { Inner = new SequenceBehavior { Items = { producer, Inv("B") } } };
        var compiled = Explorer().Compile(body);
        Assert.True(compiled.HasReturnBindings);
    }

    [Fact]
    public void Explore_MachineReference_IsResolvedAndRecursed()
    {
        // A scenario that references machine "Sub", whose body itself uses a permutation + fail.
        var machines = new Dictionary<string, Behavior>
        {
            ["Sub"] = new FailBehavior { Inner = new PermutationBehavior { Items = { Inv("A"), Inv("B") } } },
        };
        var body = new SequenceBehavior { Items = { Inv("C"), Inv("Sub") } };
        var explorer = Explorer(machines);
        var compiled = explorer.Compile(body);
        Assert.True(compiled.HasFailStates); // recursed into Sub and found the fail
    }
}
