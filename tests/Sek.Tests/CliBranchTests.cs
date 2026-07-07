using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sek.Cli;
using Sek.Cord;
using Sek.Core.Model;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Direct coverage for <see cref="Conformance"/> replay branches (malformed label, missing SUT
/// method, successful stateful replay) using crafted graphs against the built Turnstile SUT, plus
/// additional <see cref="SekCli"/> slice/param-machine transforms — reachable now that the CLI logic
/// is a library type (REF003).
/// </summary>
public class CliBranchTests : IClassFixture<SampleModelsFixture>
{
    private readonly string _turnstileSut;

    public CliBranchTests(SampleModelsFixture fx)
    {
        _turnstileSut = Path.Combine(fx.RepoRoot, "samples", "Turnstile", "Sut", "bin", "Debug", "Turnstile.Sut.dll");
    }

    private static ExplorationGraph LinearGraph(params (string from, string action, string to, bool accepting)[] edges)
    {
        var g = new ExplorationGraph { Machine = "M", InitialStateId = "S0" };
        var ids = new HashSet<string>();
        void AddState(string id, bool accepting, bool initial)
        {
            if (ids.Add(id)) g.States.Add(new ModelState(id, "h" + id, Accepting: accepting, Initial: initial));
        }
        AddState("S0", edges.Length == 0, initial: true);
        foreach (var (from, action, to, accepting) in edges)
        {
            AddState(from, false, false);
            AddState(to, accepting, false);
            g.Transitions.Add(new Transition(from, ActionInvocation.Of(action), to));
        }
        return g;
    }

    [Fact]
    public void Conformance_StatefulPath_Succeeds()
    {
        // Coin then Push against the real Turnstile SUT, replayed on one instance per path.
        var g = LinearGraph(("S0", "Turnstile.Coin", "S1", false), ("S1", "Turnstile.Push", "S2", true));
        var report = Conformance.Replay(g, _turnstileSut, "Turnstile.Sut");
        Assert.True(report.Passed, string.Join("; ", report.Failures));
        Assert.Contains("Turnstile.Coin", report.ActionsCovered);
    }

    [Fact]
    public void Conformance_MalformedLabel_IsReported()
    {
        var g = LinearGraph(("S0", "NoDotLabel", "S1", true));
        var report = Conformance.Replay(g, _turnstileSut, "Turnstile.Sut");
        Assert.False(report.Passed);
        Assert.Contains(report.Failures, m => m.Contains("malformed"));
    }

    [Fact]
    public void Conformance_MissingSutMethod_IsReported()
    {
        var g = LinearGraph(("S0", "Turnstile.DoesNotExist", "S1", true));
        var report = Conformance.Replay(g, _turnstileSut, "Turnstile.Sut");
        Assert.False(report.Passed);
        Assert.Contains(report.Failures, m => m.Contains("no SUT method"));
    }

    [Fact]
    public void Conformance_MissingBindingAssembly_Throws()
    {
        var g = LinearGraph(("S0", "Turnstile.Coin", "S1", true));
        Assert.Throws<FileNotFoundException>(() =>
            Conformance.Replay(g, Path.Combine(Path.GetTempPath(), "no-such.dll"), "Ns"));
    }

    // ---- SekCli slice / param-machine transforms ---------------------------------------

    [Fact]
    public void ExtractSlice_PlainBehavior_HasNoConfig()
    {
        var cord = CordDocument.ParseText("config C { action all S; } machine M() : C { a; b }");
        var (scenario, config) = SekCli.ExtractSlice(cord, cord.GetMachine("M")!.Body);
        Assert.NotNull(scenario);
        Assert.Null(config); // no model-program slice
    }

    [Fact]
    public void ExtractSlice_InlinesMachineReference()
    {
        var cord = CordDocument.ParseText(
            "config C { action all S; }\n" +
            "machine Mp() : C { construct model program from C }\n" +
            "machine Inner() : C { a || Mp }\n" +
            "machine Outer() : C { Inner }\n");
        var (scenario, config) = SekCli.ExtractSlice(cord, cord.GetMachine("Outer")!.Body);
        Assert.NotNull(scenario);
        Assert.Equal("C", config);
    }
}
