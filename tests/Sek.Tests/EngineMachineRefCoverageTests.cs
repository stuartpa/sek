using System.Collections.Generic;
using System.Linq;
using Sek.Cord.Ast;
using Sek.Engine;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for <see cref="BehaviorExplorer"/>'s machine-reference handling: when the same
/// machine is referenced more than once, the <c>seen</c>-set guard short-circuits the second visit
/// (in CollectSymbols / CollectReturnBindings / ContainsFail).
/// </summary>
public class EngineMachineRefCoverageTests
{
    private static InvocationBehavior Inv(string target) => new() { Target = target };

    private static BehaviorExplorer Explorer(Dictionary<string, Behavior> machines) =>
        new(name => machines.TryGetValue(name, out var b) ? b : null, new[] { "A", "B" });

    [Fact]
    public void MachineReferencedTwice_IsVisitedOnce()
    {
        // M1 is referenced twice in a sequence — the second reference is deduplicated by the
        // seen-set guard (not re-expanded for binding/fail collection).
        var machines = new Dictionary<string, Behavior> { ["M1"] = Inv("A") };
        var body = new SequenceBehavior { Items = { Inv("M1"), Inv("M1") } };
        var sc = Explorer(machines).Compile(body);
        Assert.False(sc.HasReturnBindings);
        Assert.False(sc.HasFailStates);
    }

    [Fact]
    public void MachineWithFail_ReferencedTwice_StillDetectsFail()
    {
        // A machine containing a `: fail` referenced twice: the first visit detects the fail; the
        // second is short-circuited.
        var machines = new Dictionary<string, Behavior>
        {
            ["MF"] = new FailBehavior { Inner = Inv("A") },
        };
        var body = new SequenceBehavior { Items = { Inv("MF"), Inv("MF") } };
        var sc = Explorer(machines).Compile(body);
        Assert.True(sc.HasFailStates);
    }

    [Fact]
    public void MachineWithReturnBinding_ReferencedTwice()
    {
        var machines = new Dictionary<string, Behavior>
        {
            ["MP"] = new InvocationBehavior { Target = "A", ReturnBinding = "v" },
        };
        var body = new SequenceBehavior { Items = { Inv("MP"), Inv("MP") } };
        var sc = Explorer(machines).Compile(body);
        Assert.True(sc.HasReturnBindings);
    }
}
