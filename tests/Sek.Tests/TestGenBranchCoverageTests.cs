using System;
using System.IO;
using Sek.Cli;
using Sek.Core.Model;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for <see cref="TestGen"/>'s negative-test emission and prefix routing over a
/// hand-built graph: an <c>event</c> action in the legal prefix (the <c>Observe</c> verb), a
/// call action with arguments (the argument-formatting arm), a negative transition whose action
/// carries arguments and whose reason needs string-escaping, and <see cref="TestGen.LegalPrefixTo"/>
/// for the initial, a reachable, and an unreachable state.
/// </summary>
public class TestGenBranchCoverageTests
{
    private static readonly string ThisAssembly = typeof(TestGenBranchCoverageTests).Assembly.Location;

    private static ExplorationGraph BuildGraph()
    {
        var g = new ExplorationGraph { Machine = "M", InitialStateId = "S0" };
        g.States.Add(new ModelState("S0", "h0", Label: null, Accepting: false, Initial: true));
        g.States.Add(new ModelState("S1", "h1", Label: null, Accepting: false));
        g.States.Add(new ModelState("S2", "h2", Label: null, Accepting: true));
        g.States.Add(new ModelState("S3", "h3", Label: null, Accepting: true)); // unreachable island

        // Prefix: an EVENT action then a CALL action with an argument.
        g.Transitions.Add(new Transition("S0", new ActionInvocation("Ev.Ring", Array.Empty<string>(), "event"), "S1"));
        g.Transitions.Add(new Transition("S1", new ActionInvocation("A.Do", new[] { "x" }), "S2"));

        // A model-forbidden action at S2 with an argument and a reason needing escaping.
        g.NegativeTransitions.Add(new NegativeTransition(
            "S2", new ActionInvocation("A.Blocked", new[] { "y" }), "he said \"no\"\nstop"));
        return g;
    }

    [Fact]
    public void EmitXunit_NegativeTest_UsesObserveForEvents_StepWithArgs_AndEscapesReason()
    {
        var g = BuildGraph();
        var paths = TestGen.SelectPaths(g, 5, TestGen.TestStrategy.Long);
        var outDir = Path.Combine(Path.GetTempPath(), "tg_branch_" + Guid.NewGuid().ToString("N"));
        try
        {
            var res = TestGen.EmitXunit(g, paths, outDir, "Gen.Tests", ThisAssembly, "Sek.Tests.GateSut");
            Assert.Equal(1, res.NegativeTestCount);

            var src = File.ReadAllText(res.TestFile);
            Assert.Contains("Reject_A_Blocked", src);
            Assert.Contains("_sut.Observe(\"Ev.Ring\")", src);   // event action → Observe verb
            Assert.Contains("_sut.Step(\"A.Do\", \"x\")", src);  // call action with an argument
            Assert.Contains("StepExpectingError(\"A.Blocked\"", src);
            Assert.Contains("\"y\"", src);                        // negative action argument
            Assert.Contains("\\\"no\\\"", src);                   // escaped quotes in the reason
        }
        finally
        {
            try { Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void LegalPrefixTo_Initial_Reachable_Unreachable()
    {
        var g = BuildGraph();
        Assert.Empty(TestGen.LegalPrefixTo(g, "S0"));      // the initial state → no prefix
        Assert.NotEmpty(TestGen.LegalPrefixTo(g, "S2"));   // reachable via S0→S1→S2
        Assert.Empty(TestGen.LegalPrefixTo(g, "S3"));      // unreachable island → empty
    }
}
