using System.Collections.Generic;
using System.Linq;
using Sek.Cord.Ast;
using Sek.Engine;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for <see cref="BehaviorExplorer"/>'s scenario compilation
/// (<c>Compile</c>/<c>ToDfa</c> and the recursive walkers <c>CollectReturnBindings</c>,
/// <c>ContainsFail</c>, <c>CollectSymbols</c>): every behavior-operator node kind (sequence,
/// choice, parallel, permutation, loose-sequence, group, repetition, preconstraint, let, bind,
/// fail), machine-reference recursion, negation and the <c>_</c> wildcard, model-checking fail
/// states, and return-bindings nested under each operator.
/// </summary>
public class EngineAutomatonBranchTests
{
    private static InvocationBehavior Inv(string target, params string[] args) =>
        new() { Target = target, Args = args.Length == 0 ? null : args.ToList() };

    private static InvocationBehavior Producer(string target, string bind) =>
        new() { Target = target, ReturnBinding = bind };

    private static BehaviorExplorer Plain() => new(_ => null, new[] { "A", "B", "C" });

    private static BehaviorExplorer WithMachines(Dictionary<string, Behavior> machines) =>
        new(name => machines.TryGetValue(name, out var b) ? b : null, new[] { "A", "B", "C" });

    [Fact]
    public void Compile_EveryOperator_WithoutError()
    {
        Behavior[] bodies =
        {
            new ParallelBehavior { Items = { Inv("A"), Inv("B") }, Op = "sync" },
            new PermutationBehavior { Items = { Inv("A"), Inv("B") } },
            new LooseSequenceBehavior { Items = { Inv("A"), Inv("B") } },
            new GroupBehavior { Inner = new SequenceBehavior { Items = { Inv("A"), Inv("B") } } },
            new RepetitionBehavior { Inner = Inv("A"), Op = "*" },
            new RepetitionBehavior { Inner = Inv("A"), Op = "+" },
            new RepetitionBehavior { Inner = Inv("A"), Op = "?" },
            new RepetitionBehavior { Inner = Inv("A"), Op = "{n,m}", Min = 2, Max = 4 },
            new PreconstraintBehavior { Code = "true", Inner = Inv("A") },
            new LetBehavior { Inner = Inv("A"), WhereCode = "Condition.In(x,1)" },
            new BindBehavior { Inner = Inv("A") },
        };

        foreach (var body in bodies)
        {
            var ex = Record.Exception(() => Plain().Compile(body));
            Assert.True(ex is null, $"{body.GetType().Name} failed to compile: {ex?.Message}");
        }
    }

    [Fact]
    public void Compile_FailBehavior_MarksFailStates()
    {
        var sc = Plain().Compile(new FailBehavior { Inner = Inv("A") });
        Assert.True(sc.HasFailStates);
        Assert.True(sc.TryStep(sc.Start, "A", out var s1));
        Assert.True(sc.IsFail(s1));
    }

    [Fact]
    public void Compile_Wildcard_PermitsAnyAction()
    {
        var wild = Plain().Compile(new SequenceBehavior { Items = { Inv("_"), Inv("B") } });
        Assert.True(wild.Permits(wild.Start, "A"));
        Assert.True(wild.Permits(wild.Start, "C"));
    }

    [Fact]
    public void Compile_NegatedInvocation_Compiles()
    {
        var neg = Plain().Compile(new SequenceBehavior
        {
            Items = { new InvocationBehavior { Target = "A", Negated = true }, Inv("B") },
        });
        // !A permits any action other than A from the start state.
        Assert.True(neg.Permits(neg.Start, "B"));
    }

    [Fact]
    public void Compile_MachineReference_RecursesForBindingsAndFail()
    {
        // Machine P = A / v ; B(v) — a return binding inside a referenced machine.
        var pBody = new SequenceBehavior
        {
            Items = { Producer("A", "v"), Inv("B", "v") },
        };
        // Machine Q contains a fail annotation.
        var qBody = new FailBehavior { Inner = Inv("C") };
        var machines = new Dictionary<string, Behavior> { ["P"] = pBody, ["Q"] = qBody };
        var explorer = WithMachines(machines);

        var withBinding = explorer.Compile(new ParallelBehavior { Items = { Inv("P"), Inv("C") } });
        Assert.True(withBinding.HasReturnBindings);

        var withFail = explorer.Compile(new SequenceBehavior { Items = { Inv("A"), Inv("Q") } });
        Assert.True(withFail.HasFailStates);
    }

    [Fact]
    public void Compile_ReturnBindings_NestedUnderEachOperator()
    {
        var producers = new (string Name, Behavior Body)[]
        {
            ("permutation", new PermutationBehavior { Items = { Producer("A", "v"), Inv("B") } }),
            ("loose-sequence", new LooseSequenceBehavior { Items = { Producer("A", "v"), Inv("B") } }),
            ("group", new GroupBehavior { Inner = Producer("A", "v") }),
            ("repetition", new RepetitionBehavior { Inner = Producer("A", "v"), Op = "+" }),
            ("preconstraint", new PreconstraintBehavior { Code = "true", Inner = Producer("A", "v") }),
            ("let", new LetBehavior { Inner = Producer("A", "v") }),
            ("bind", new BindBehavior { Inner = Producer("A", "v") }),
            ("fail", new FailBehavior { Inner = Producer("A", "v") }),
            ("parallel", new ParallelBehavior { Items = { Producer("A", "v"), Inv("B") } }),
        };

        foreach (var (name, body) in producers)
        {
            var sc = Plain().Compile(body);
            Assert.True(sc.HasReturnBindings, $"expected return-bindings under {name}");
        }
    }
}
