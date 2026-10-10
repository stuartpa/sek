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
            Assert.Contains("AppContext.BaseDirectory", src);
            Assert.Contains("BindingAssets", src);
            Assert.DoesNotContain(ThisAssembly, src, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SEK_BINDING", src, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(outDir, "BindingAssets", Path.GetFileName(ThisAssembly))));
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

    [Fact]
    public void ConcreteWitnesses_DistinguishUnreachable_AndGeneratedHarnessRejectsSetupErrors()
    {
        var graph = BuildGraph();
        Assert.Null(TestGen.ConcretePrefixTo(graph, "S3"));
        Assert.Null(TestGen.ConcreteSuffixToAccepting(graph, "absent"));
        Assert.Equal(2, TestGen.ConcretePrefixTo(graph, "S2")!.Count);
        var output = Path.Combine(Path.GetTempPath(), "sek_harness_" + Guid.NewGuid().ToString("N"));
        try
        {
            var generated = TestGen.EmitXunit(graph, TestGen.SelectPaths(graph, 5), output, "Gen.Tests", ThisAssembly, "Sek.Tests.GateSut");
            var source = File.ReadAllText(generated.TestFile);
            Assert.Contains("catch (TargetInvocationException", source);
            Assert.DoesNotContain("catch (Exception) { rejected = true; }", source);
            Assert.Contains("Assert.Equal(ps.Length, args.Length)", source);
            Assert.Contains("Assert.Equal(expectedExceptionType, actual.GetType().FullName)", source);
            Assert.Contains("Assert.Contains(expectedMessageContains, actual.Message)", source);
        }
        finally { Directory.Delete(output, true); }
    }

    [Fact]
    public void StructuredProof_SelectsExactArgumentsAndJsonState_AndReportsMissingIds()
    {
        var root = Path.Combine(Path.GetTempPath(), "sek_selection_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var item = new ProofCase { Id = "exact", Action = "A.Do", Arguments = new() { "x" },
                FromState = new() { ["Count"] = System.Text.Json.JsonSerializer.SerializeToElement(1) } };
            var contract = new ProofContract { SchemaVersion = "1.0", Builds = new() { new() { Project = "Model.csproj", Inputs = new() { "." } } },
                Machines = new() { ["M"] = new() { Actions = new() { "Ev.Ring", "A.Do", "A.Blocked" }, PositiveCases = new() { item }, RequiredActions = new() { "A.Do" } } } };
            var config = new ProjectConfig { Proof = "proof.json" };
            void Save() => File.WriteAllText(Path.Combine(root, "proof.json"), System.Text.Json.JsonSerializer.Serialize(contract,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
            Save();
            var graph = BuildGraph();
            graph.Metadata["proofStateJson"] = System.Text.Json.JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, string> { ["S1"] = "{\"Count\":1}" });
            var proof = new ProofEvidence(config, root);
            Assert.Throws<InvalidDataException>(() => proof.VerifyActions("M", new[] { "A.Do" }));
            Assert.Throws<InvalidDataException>(() => proof.Machine("m"));
            var selection = proof.Select(graph, "M");
            Assert.True(selection.Complete);
            Assert.Equal(new[] { "exact" }, selection.CoveredPositiveIds);
            Assert.Equal(2, Assert.Single(selection.Paths).Steps.Count);
            item.Arguments = new() { "wrong" };
            Save();
            Assert.Contains("exact", new ProofEvidence(config, root).Select(graph, "M").MissingPositiveIds);
            item.Arguments = new() { "x" };
            item.FromState!["Count"] = System.Text.Json.JsonSerializer.SerializeToElement(2);
            Save();
            Assert.Contains("exact", new ProofEvidence(config, root).Select(graph, "M").MissingPositiveIds);
        }
        finally { Directory.Delete(root, true); }
    }
}
