using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Sek.Cli;
using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Core.Model;
using Sek.Engine;
using Sek.Modeling;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

public sealed class ProofEvidenceCoverageTests
{
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "sek_evidence_" + Guid.NewGuid().ToString("N"));
        public string ProofPath => Path.Combine(Root, "proof.json");
        public ProjectConfig Config { get; }
        public ProofContract Contract { get; }

        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Model"));
            Directory.CreateDirectory(Path.Combine(Root, ".specexplorerkit"));
            File.WriteAllText(Path.Combine(Root, "Model", "Pilot.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(Root, "Model", "Pilot.cs"), "public sealed class Pilot { }\n");
            File.WriteAllText(Path.Combine(Root, "Model", "Pilot.cord"), "config C { action all T; }\n");
            Config = new ProjectConfig
            {
                Model = new() { Assembly = "Model/bin/Debug/net10.0/Pilot.dll", Type = "Pilot" },
                Cord = "Model", Proof = "proof.json",
            };
            File.WriteAllText(Path.Combine(Root, ".specexplorerkit", "config.json"), JsonSerializer.Serialize(Config));
            Contract = new ProofContract
            {
                SchemaVersion = "1.0",
                Builds = new() { new() { Project = "Model/Pilot.csproj", Inputs = new() { "Model" } } },
                Machines = new()
                {
                    ["M"] = new()
                    {
                        Actions = new() { "T.a", "T.bad" }, RequiredActions = new() { "T.a", "T.bad" },
                        PositiveCases = new() { new() { Id = "legal", Action = "T.a", Arguments = new() } },
                        Rejections = new() { new() { Id = "reject", Action = "T.bad", Arguments = new() { "1" },
                            ObligationId = "forbidden", ExpectedExceptionType = "System.InvalidOperationException",
                            ExpectedMessageContains = "blocked" } },
                    },
                },
            };
            Save();
        }

        public void Save() => File.WriteAllText(ProofPath, JsonSerializer.Serialize(Contract,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        public ProofEvidence Open() => new(Config, Root);
        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (Exception error) when ((error is IOException or UnauthorizedAccessException)
                && AppDomain.CurrentDomain.GetAssemblies().Any(assembly => !assembly.IsDynamic
                    && assembly.Location.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
            }
        }
    }

    private static ExplorationGraph Graph(string machine = "M")
    {
        var graph = new ExplorationGraph { Machine = machine, InitialStateId = "S0" };
        graph.States.Add(new ModelState("S0", "h0", Label: "{\"Phase\":0}", Initial: true));
        graph.States.Add(new ModelState("S1", "h1", Label: "{\"Phase\":1}", Accepting: true));
        graph.Transitions.Add(new Transition("S0", ActionInvocation.Of("T.a"), "S1"));
        graph.NegativeTransitions.Add(new NegativeTransition("S1", ActionInvocation.Of("T.bad", "1"), "blocked")
            { ObligationId = "forbidden" });
        return graph;
    }

    [Theory]
    [InlineData("states", 0)]
    [InlineData("states", 5001)]
    [InlineData("transitions", 0)]
    [InlineData("transitions", 20001)]
    [InlineData("negatives", 0)]
    [InlineData("negatives", 5001)]
    [InlineData("candidates", 0)]
    [InlineData("candidates", 250001)]
    [InlineData("explorationSeconds", 0)]
    [InlineData("explorationSeconds", 121)]
    [InlineData("phaseSeconds", 0)]
    [InlineData("phaseSeconds", 301)]
    [InlineData("graphBytes", 0)]
    [InlineData("graphBytes", 26214401)]
    public void ContractRejectsEachBudgetBoundary(string field, int value)
    {
        using var fixture = new Fixture();
        var contract = JsonNode.Parse(File.ReadAllText(fixture.ProofPath))!;
        contract["budgets"]![field] = value;
        File.WriteAllText(fixture.ProofPath, contract.ToJsonString());
        Assert.Contains("safety ceilings", Assert.Throws<InvalidDataException>(fixture.Open).Message);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("empty-machines")]
    [InlineData("empty-machine-name")]
    [InlineData("empty-builds")]
    [InlineData("empty-actions")]
    [InlineData("blank-action")]
    [InlineData("duplicate-actions")]
    [InlineData("unknown-required-action")]
    [InlineData("duplicate-case")]
    [InlineData("unknown-case-action")]
    [InlineData("blank-obligation")]
    [InlineData("blank-exception")]
    [InlineData("unqualified-exception")]
    [InlineData("non-project-build")]
    [InlineData("empty-build-inputs")]
    [InlineData("absolute-proof-path")]
    public void ContractRejectsMalformedExactScope(string defect)
    {
        using var fixture = new Fixture();
        var contract = fixture.Contract;
        var machine = contract.Machines["M"];
        switch (defect)
        {
            case "schema": contract.SchemaVersion = "2.0"; break;
            case "empty-machines": contract.Machines.Clear(); break;
            case "empty-machine-name": contract.Machines[""] = machine; break;
            case "empty-builds": contract.Builds.Clear(); break;
            case "empty-actions": machine.Actions.Clear(); break;
            case "blank-action": machine.Actions.Add(" "); break;
            case "duplicate-actions": machine.Actions.Add("T.a"); break;
            case "unknown-required-action": machine.RequiredActions.Add("T.unknown"); break;
            case "duplicate-case": machine.Rejections[0].Id = "legal"; break;
            case "unknown-case-action": machine.PositiveCases[0].Action = "T.unknown"; break;
            case "blank-obligation": machine.Rejections[0].ObligationId = ""; break;
            case "blank-exception": machine.Rejections[0].ExpectedExceptionType = ""; break;
            case "unqualified-exception": machine.Rejections[0].ExpectedExceptionType = "InvalidOperationException"; break;
            case "non-project-build": contract.Builds[0].Project = "Model/Pilot.txt"; break;
            case "empty-build-inputs": contract.Builds[0].Inputs.Clear(); break;
            case "absolute-proof-path": fixture.Config.Proof = fixture.ProofPath; break;
        }
        fixture.Save();
        Assert.Throws<InvalidDataException>(fixture.Open);
    }

    [Fact]
    public void ContractRejectsDuplicateNestedFieldsAndUnknownProperties()
    {
        using var fixture = new Fixture();
        var original = File.ReadAllText(fixture.ProofPath);
        File.WriteAllText(fixture.ProofPath, original.Insert(1, "\"schemaVersion\":\"1.0\","));
        Assert.Contains("duplicate proof field", Assert.Throws<InvalidDataException>(fixture.Open).Message);
        File.WriteAllText(fixture.ProofPath, original.Replace("\"states\":5000", "\"states\":5000,\"states\":5000"));
        Assert.Contains("duplicate proof field", Assert.Throws<InvalidDataException>(fixture.Open).Message);
        var node = JsonNode.Parse(original)!;
        node["unsupported"] = true;
        File.WriteAllText(fixture.ProofPath, node.ToJsonString());
        Assert.Throws<JsonException>(fixture.Open);
        fixture.Config.Proof = null;
        Assert.Throws<InvalidDataException>(fixture.Open);
    }

    [Theory]
    [InlineData("")]
    [InlineData("$(Dependency).csproj")]
    [InlineData("*.csproj")]
    [InlineData("First.csproj;Second.csproj")]
    public void BuildRejectsAmbiguousProjectReferenceBeforeAttesting(string include)
    {
        using var fixture = new Fixture();
        new XDocument(new XElement("Project", new XElement("ItemGroup",
            new XElement("ProjectReference", new XAttribute("Include", include)))))
            .Save(Path.Combine(fixture.Root, "Model", "Pilot.csproj"));
        var error = Assert.Throws<InvalidDataException>(() => fixture.Open().Build());
        Assert.Contains("ambiguous project reference", error.Message);
        Assert.False(File.Exists(Path.Combine(fixture.Root, ".specexplorerkit", "out", "build-attestation.json")));
    }

    [Fact]
    public void BuildRejectsMissingTransitiveProjectBeforeAttesting()
    {
        using var fixture = new Fixture();
        new XDocument(new XElement("Project", new XElement("ItemGroup",
            new XElement("ProjectReference", new XAttribute("Include", "Absent.csproj")))))
            .Save(Path.Combine(fixture.Root, "Model", "Pilot.csproj"));
        Assert.Contains("build project missing", Assert.Throws<FileNotFoundException>(() => fixture.Open().Build()).Message);
    }

    [Fact]
    public void BuildRejectsMissingDeclaredInputBeforeAttesting()
    {
        using var fixture = new Fixture();
        fixture.Contract.Inputs.Add("absent-input.py");
        fixture.Save();
        Assert.Contains("proof input missing", Assert.Throws<FileNotFoundException>(() => fixture.Open().Build()).Message);
    }

    [Theory]
    [InlineData("missing-state")]
    [InlineData("null-label")]
    [InlineData("missing-field")]
    [InlineData("wrong-value")]
    [InlineData("wrong-arguments")]
    [InlineData("no-accepting-suffix")]
    public void SelectionRejectsMismatchedStateAndLegalBoundary(string defect)
    {
        using var fixture = new Fixture();
        var legal = fixture.Contract.Machines["M"].PositiveCases[0];
        legal.FromState = new() { ["Phase"] = JsonSerializer.SerializeToElement(0) };
        var graph = Graph();
        switch (defect)
        {
            case "missing-state": graph.States.RemoveAt(0); break;
            case "null-label": graph.States[0] = graph.States[0] with { Label = null }; break;
            case "missing-field": legal.FromState["Unknown"] = JsonSerializer.SerializeToElement(0); break;
            case "wrong-value": legal.FromState["Phase"] = JsonSerializer.SerializeToElement(1); break;
            case "wrong-arguments": legal.Arguments = new() { "unexpected" }; break;
            case "no-accepting-suffix": graph.States[1] = graph.States[1] with { Accepting = false }; break;
        }
        fixture.Save();
        Assert.Contains("legal", fixture.Open().Select(graph, "M").MissingPositiveIds);
    }

    [Fact]
    public void SelectionSupportsWildcardArgumentsAndEmptyStatePredicates()
    {
        using var fixture = new Fixture();
        fixture.Contract.Machines["M"].PositiveCases[0].Arguments = null;
        fixture.Contract.Machines["M"].PositiveCases[0].FromState = new();
        fixture.Save();
        Assert.True(fixture.Open().Select(Graph(), "M").Complete);
        var options = new ExplorationOptions { MaxStates = 2, MaxTransitions = 3 };
        fixture.Contract.Budgets.Apply(options);
        Assert.Equal(2, options.MaxStates);
        Assert.Equal(3, options.MaxTransitions);
        Assert.Equal(TimeSpan.FromSeconds(120), options.MaxElapsedTime);
    }

    [Fact]
    public void DirectSelectionRequiresReachableExactLegalAndRejectionWitnesses()
    {
        using var fixture = new Fixture();
        var machine = fixture.Contract.Machines["M"];
        machine.PositiveCases[0].FromState = new() { ["Phase"] = JsonSerializer.SerializeToElement(0) };
        machine.Rejections[0].FromState = new() { ["Phase"] = JsonSerializer.SerializeToElement(1) };
        fixture.Save();
        var evidence = fixture.Open();
        evidence.VerifyActions("M", new[] { "T.bad", "T.a" });
        Assert.Throws<InvalidDataException>(() => evidence.VerifyActions("M", new[] { "T.a" }));
        Assert.Throws<InvalidDataException>(() => evidence.Machine("unknown"));
        var graph = Graph();
        var selection = evidence.Select(graph, "M");
        Assert.True(selection.Complete);
        Assert.Equal(new[] { "legal" }, selection.CoveredPositiveIds);
        Assert.Equal(new[] { "reject" }, selection.CoveredRejectionIds);
        Assert.Equal("System.InvalidOperationException", Assert.Single(selection.Negatives).ExpectedExceptionType);
        Assert.Equal("blocked", Assert.Single(selection.Negatives).ExpectedMessageContains);
        graph.NegativeTransitions[0] = graph.NegativeTransitions[0] with { ExpectedExceptionType = "System.ArgumentException" };
        Assert.Contains("reject", evidence.Select(graph, "M").MissingRejectionIds);
        graph.NegativeTransitions[0] = graph.NegativeTransitions[0] with
            { ExpectedExceptionType = "System.InvalidOperationException", ExpectedMessageContains = "wrong" };
        Assert.Contains("reject", evidence.Select(graph, "M").MissingRejectionIds);
        graph.NegativeTransitions[0] = graph.NegativeTransitions[0] with { ExpectedMessageContains = "blocked" };
        Assert.True(evidence.Select(graph, "M").Complete);
        graph.Metadata["proofStateJson"] = "{\"S0\":\"{\\\"Phase\\\":2}\"}";
        Assert.Contains("legal", evidence.Select(graph, "M").MissingPositiveIds);
        graph.Metadata.Remove("proofStateJson");
        graph.States[0] = graph.States[0] with { Label = "invalid-json" };
        Assert.Contains("legal", evidence.Select(graph, "M").MissingPositiveIds);
        graph.Transitions.Clear();
        selection = evidence.Select(graph, "M");
        Assert.False(selection.Complete);
        Assert.Contains("accepting-legal-nonzero-result", selection.MissingPositiveIds);
        Assert.Contains("reject", selection.MissingRejectionIds);
    }

    [Fact]
    public void DirectReportPreservesMissingObligationsAndIncompleteResults()
    {
        using var fixture = new Fixture();
        var evidence = fixture.Open();
        var path = Path.Combine(fixture.Root, ".specexplorerkit", "out", "report.json");
        evidence.Report(path, "FAIL", "M", null, TimeSpan.FromSeconds(1), error: "unattested");
        using (var report = JsonDocument.Parse(File.ReadAllText(path)))
        {
            Assert.Equal("INCOMPLETE", report.RootElement.GetProperty("completeness").GetString());
            Assert.Equal("legal", report.RootElement.GetProperty("missingPositiveIds")[0].GetString());
            Assert.Equal("reject", report.RootElement.GetProperty("missingRejectionIds")[0].GetString());
            Assert.Equal("unattested", report.RootElement.GetProperty("error").GetString());
        }
        var graph = Graph();
        graph.Metadata["candidateEvaluations"] = "11";
        graph.Metadata["elapsedMilliseconds"] = "12.5";
        evidence.Select(graph, "M");
        evidence.Report(path, "PASS", "M", new ExplorationResult { Graph = graph }, TimeSpan.Zero,
            new ConformanceReport { TransitionsReplayed = 1, Succeeded = 1, NegativeReplayed = 1, NegativeRejected = 1 });
        using var complete = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("COMPLETE", complete.RootElement.GetProperty("completeness").GetString());
        Assert.Equal(11, complete.RootElement.GetProperty("budgetCounters").GetProperty("candidateEvaluations").GetInt64());
        Assert.Equal(12.5, complete.RootElement.GetProperty("budgetCounters").GetProperty("elapsedMilliseconds").GetDouble());
        Assert.True(complete.RootElement.GetProperty("replayResults").GetProperty("passed").GetBoolean());
        evidence.Report(path, "FAIL", "unknown", null, TimeSpan.Zero);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("schema")]
    [InlineData("machine")]
    [InlineData("fingerprint")]
    [InlineData("digest")]
    [InlineData("graph-machine")]
    [InlineData("incomplete")]
    [InlineData("oversized")]
    public void CheckpointReuseRequiresExactCompleteIdentity(string defect)
    {
        using var fixture = new Fixture();
        var evidence = fixture.Open();
        var graph = Graph(defect == "graph-machine" ? "Other" : "M");
        graph.Metadata["completeness"] = defect == "incomplete" ? "INCOMPLETE" : "COMPLETE";
        graph.Metadata["proofStateJson"] = "{\"S0\":\"{}\"}";
        graph.Metadata["proofRequirements"] = "[\"required\"]";
        var path = Path.Combine(fixture.Root, "checkpoint.seexpl");
        Sek.Core.Seexpl.SeexplDocument.SaveGraph(graph, path);
        var checkpoint = new JsonObject
        {
            ["schemaVersion"] = defect == "schema" ? "2.0" : "1.0",
            ["machine"] = defect == "machine" ? "Other" : "M",
            ["inputFingerprint"] = defect == "fingerprint" ? "stale" : "",
            ["graphHash"] = defect == "digest" ? "tampered" : ProofEvidence.Hash(path),
        };
        File.WriteAllText(path + ".proof.json", checkpoint.ToJsonString());
        if (defect == "oversized") evidence.Contract.Budgets.GraphBytes = 1;
        if (defect != "valid")
        {
            Assert.Throws<InvalidDataException>(() => evidence.LoadCheckpoint(path, "M"));
            Assert.False(evidence.Reused);
            return;
        }
        var result = evidence.LoadCheckpoint(path, "M");
        Assert.True(evidence.Reused);
        Assert.Equal("{}", result.StateJson["S0"]);
        Assert.Contains("required", result.CapturedRequirements);
        Assert.Equal(ProofEvidence.Hash(path), evidence.Artifacts[Path.GetFullPath(path)]);
    }

    [Fact]
    public void BuildRejectsOversizedChildOutputWithoutPublishingAttestation()
    {
        using var fixture = new Fixture();
        var projectPath = Path.Combine(fixture.Root, "Model", "Pilot.csproj");
        var project = XDocument.Load(projectPath);
        project.Root!.Add(new XElement("Target", new XAttribute("Name", "NoisyBuild"),
            new XAttribute("BeforeTargets", "BeforeBuild"),
            new XElement("Message", new XAttribute("Importance", "high"),
                new XAttribute("Text", new string('x', 2 * 1024 * 1024 + 1)))));
        project.Save(projectPath);
        var error = Assert.Throws<InvalidDataException>(() => fixture.Open().Build());
        Assert.Contains("INCOMPLETE", error.Message);
        Assert.Contains("log-limit", error.Message);
        Assert.False(File.Exists(Path.Combine(fixture.Root, ".specexplorerkit", "out", "build-attestation.json")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Out")]
    [InlineData("Error")]
    public void StrictReplayCommand_UsesRealBindingAndRejectsStaleSource(string? noisyStream)
    {
        using var fixture = new Fixture();
        var assemblyName = "Pilot" + Guid.NewGuid().ToString("N");
        fixture.Config.Model.Assembly = $"Model/bin/Debug/net10.0/{assemblyName}.dll";
        var project = new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0"),
                new XElement("AssemblyName", assemblyName)),
            new XElement("ItemGroup", new XElement("Reference", new XAttribute("Include", "Sek.Modeling"),
                new XElement("HintPath", typeof(ModelProgram).Assembly.Location)))));
        project.Save(Path.Combine(fixture.Root, "Model", "Pilot.csproj"));
        var source = Path.Combine(fixture.Root, "Model", "Pilot.cs");
        File.WriteAllText(source, """
            using Sek.Modeling;
            public sealed class Pilot : ModelProgram
            {
                public bool Done { get; set; }
                [Rule("T.a")]
                public void Advance() { RequireBound(!Done, "single journey"); Done = true; }
                [Rule("T.bad")]
                public void Reject([Domain(nameof(Values))] int value)
                {
                    Require(false, "blocked", "forbidden");
                }
                public static int[] Values() => new[] { 1 };
                [AcceptingCondition]
                public bool Accepting() => Done;
            }
            public sealed class T
            {
                public void a() { }
                public void bad(int value) => throw new System.InvalidOperationException("blocked");
            }
            """);
        if (noisyStream is not null)
            File.WriteAllText(source, File.ReadAllText(source).Replace("public void a() { }",
                $"public void a() => System.Console.{noisyStream}.Write(new string('x', 2097153));"));
        File.WriteAllText(Path.Combine(fixture.Root, "Model", "Pilot.cord"), """
            config C { action void T.a(); action void T.bad(int value); }
            machine M() : C { construct model program from C }
            """);
        fixture.Config.Binding = new() { Assembly = fixture.Config.Model.Assembly, Namespace = "" };
        File.WriteAllText(Path.Combine(fixture.Root, ".specexplorerkit", "config.json"),
            JsonSerializer.Serialize(fixture.Config));
        fixture.Open().Build();

        var reportPath = Path.Combine(fixture.Root, ".specexplorerkit", "out", "M.proof-report.json");
        if (noisyStream is not null)
        {
            var refused = CliHost.Run("test", "M", "--project", fixture.Root);
            using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
            if (noisyStream == "Out")
            {
                Assert.Equal(0, refused.Code);
                Assert.DoesNotContain(new string('x', 1024), refused.Out);
                Assert.Equal("PASS", report.RootElement.GetProperty("verdict").GetString());
            }
            else
            {
                Assert.Equal(1, refused.Code);
                Assert.Contains("log-limit", refused.Err);
                Assert.Equal("INCOMPLETE", report.RootElement.GetProperty("verdict").GetString());
            }
            return;
        }

        var first = CliHost.Run("__proof-replay", "M", "--project", fixture.Root);
        Assert.Equal(0, first.Code);
        Assert.Contains("COMPLETE", first.Out);
        using (var report = JsonDocument.Parse(File.ReadAllText(reportPath)))
        {
            Assert.Equal("PASS", report.RootElement.GetProperty("verdict").GetString());
            Assert.True(report.RootElement.GetProperty("replayResults").GetProperty("passed").GetBoolean());
            Assert.Equal("reject", report.RootElement.GetProperty("coveredRejectionIds")[0].GetString());
        }

        var reused = CliHost.Run("__proof-replay", "M", "--project", fixture.Root,
            "--reuse", ".specexplorerkit/out/M.seexpl");
        Assert.Equal(0, reused.Code);
        using (var report = JsonDocument.Parse(File.ReadAllText(reportPath)))
        {
            Assert.True(report.RootElement.GetProperty("explorationReused").GetBoolean());
            Assert.False(report.RootElement.GetProperty("replayReused").GetBoolean());
        }

        File.AppendAllText(source, "\n");
        var stale = CliHost.Run("__proof-replay", "M", "--project", fixture.Root,
            "--reuse", ".specexplorerkit/out/M.seexpl");
        Assert.Equal(1, stale.Code);
        Assert.Contains("stale build", stale.Err);
        using var failed = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.Equal("FAIL", failed.RootElement.GetProperty("verdict").GetString());
    }

    [Fact]
    public void DirectAttestationAndCheckpointRejectChangedSourcesAndArtifacts()
    {
        using var fixture = new Fixture();
        var evidence = fixture.Open();
        Assert.Contains("unattested", Assert.Throws<InvalidDataException>(evidence.VerifyBuild).Message);
        evidence.Build();
        evidence.VerifyBuild();
        Assert.Matches("^[0-9a-f]{64}$", evidence.Fingerprint);
        var graph = Graph();
        graph.Metadata["proofStateJson"] = "{\"S0\":\"{}\"}";
        graph.Metadata["proofRequirements"] = "[\"REQ:legal\"]";
        var path = Path.Combine(fixture.Root, ".specexplorerkit", "out", "M.seexpl");
        evidence.SaveCheckpoint(graph, path, default);
        var loaded = evidence.LoadCheckpoint(path, "M");
        Assert.True(evidence.Reused);
        Assert.Equal("{}", loaded.StateJson["S0"]);
        Assert.Contains("REQ:legal", loaded.CapturedRequirements);
        Assert.Throws<InvalidDataException>(() => evidence.LoadCheckpoint(path, "other"));
        var original = File.ReadAllBytes(path);
        File.AppendAllText(path, " ");
        Assert.Throws<InvalidDataException>(() => evidence.LoadCheckpoint(path, "M"));
        File.WriteAllBytes(path, original);
        var source = Path.Combine(fixture.Root, "Model", "Pilot.cs");
        File.AppendAllText(source, "\n");
        Assert.Contains("stale build", Assert.Throws<InvalidDataException>(evidence.VerifyBuild).Message);
    }
}

/// <summary>
/// Branch-coverage for the extracted <see cref="SekCli"/> command handlers and the recursive
/// Cord AST transforms (scope resolution, strategy lookup, slice extraction, let-desugaring,
/// param-machine expansion, substitution). These exercise the rarer behavior-node kinds
/// (permutation / loose-sequence / fail / preconstraint) and the command error paths that the
/// sample explorations do not reach.
/// </summary>
public class SekCliCoverageTests : IClassFixture<SampleModelsFixture>
{
    private readonly SampleModelsFixture _fx;
    private readonly string _turnstile;

    public SekCliCoverageTests(SampleModelsFixture fx)
    {
        _fx = fx;
        _turnstile = Path.Combine(fx.RepoRoot, "samples", "Turnstile");
    }

    private static CordDocument Cord(string text) => CordDocument.ParseText(text);

    [Fact]
    public void IncompleteExploration_RefusesProofAndMarksDiagnosticExport()
    {
        var result = new ExplorationResult { Graph = new ExplorationGraph { Machine = "Bounded" }, HitBound = true };
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".seexpl");
        try
        {
            Assert.Throws<InvalidOperationException>(() => SekCli.RequireComplete(result));
            Assert.False(File.Exists(path));
            Assert.Throws<InvalidOperationException>(() => SekCli.RequireComplete(result, path));
            Assert.Contains("INCOMPLETE", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void StrictProof_BuildFreshnessExactCasesAndUnchangedCheckpointReuse()
    {
        (int Code, string Out, string Err) Run(params string[] arguments)
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            };
            start.ArgumentList.Add(Path.Combine(_fx.RepoRoot, "src", "Sek.Cli", "bin", "Debug", "net10.0", "sek.dll"));
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(5));
            process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
            return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
        }
        void Build(string directory)
        {
            var result = Run("build", "--project", directory);
            Assert.True(result.Code == 0, result.Err);
        }
        var root = Path.Combine(Path.GetTempPath(), "sek_proof_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, ".specexplorerkit"));
        Directory.CreateDirectory(Path.Combine(root, "Model"));
        try
        {
            var modeling = System.Security.SecurityElement.Escape(typeof(ModelProgram).Assembly.Location);
            File.WriteAllText(Path.Combine(root, "Model", "Pilot.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
                  <ItemGroup><Reference Include="Sek.Modeling"><HintPath>{modeling}</HintPath></Reference></ItemGroup>
                </Project>
                """);
            var source = Path.Combine(root, "Model", "Pilot.cs");
            File.WriteAllText(source, """
                using Sek.Modeling;
                namespace Pilot;
                public sealed class Model : ModelProgram
                {
                    public bool Done { get; set; }
                    [Rule("W.Do")] public void Do() { Done = true; }
                    [AcceptingCondition] public bool Accept() => Done;
                }
                public static class W { public static void Do() { } }
                """);
            File.WriteAllText(Path.Combine(root, "Model", "Pilot.cord"), "config C { action all W; }\nmachine M() : C { construct model program from C }\n");
            File.WriteAllText(Path.Combine(root, ".specexplorerkit", "config.json"), """
                {"model":{"assembly":"Model/bin/Debug/net10.0/Pilot.dll","type":"Pilot.Model"},"cord":"Model",
                 "binding":{"assembly":"Model/bin/Debug/net10.0/Pilot.dll","namespace":"Pilot"},"proof":"proof.json"}
                """);
            File.WriteAllText(Path.Combine(root, "sut.py"), "VALUE = 1\n");
            var proofPath = Path.Combine(root, "proof.json");
            var contract = new ProofContract
            {
                SchemaVersion = "1.0", Builds = new() { new() { Project = "Model/Pilot.csproj", Inputs = new() { "Model" } } },
                Inputs = new() { "sut.py" },
                Machines = new() { ["M"] = new() { Actions = new() { "W.Do" }, RequiredActions = new() { "W.Do" },
                    PositiveCases = new() { new() { Id = "do", Action = "W.Do", Arguments = new() } } } },
            };
            void SaveContract() => File.WriteAllText(proofPath, System.Text.Json.JsonSerializer.Serialize(contract,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
            SaveContract();
            var unattested = Run("test", "M", "--project", root);
            Assert.Equal(1, unattested.Code);
            Assert.Contains("unattested", unattested.Err);
            var build = Run("build", "--project", root);
            Assert.True(build.Code == 0, build.Err);
            var first = Run("test", "M", "--project", root);
            Assert.True(first.Code == 0, first.Err);
            var graph = Path.Combine(root, ".specexplorerkit", "out", "M.seexpl");
            var hash = ProofEvidence.Hash(graph);
            var second = Run("test", "M", "--project", root, "--reuse", graph);
            Assert.True(second.Code == 0, second.Err);
            Assert.Equal(hash, ProofEvidence.Hash(graph));
            using (var report = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".specexplorerkit", "out", "M.proof-report.json"))))
            {
                Assert.True(report.RootElement.GetProperty("explorationReused").GetBoolean());
                Assert.False(report.RootElement.GetProperty("replayReused").GetBoolean());
                Assert.True(report.RootElement.GetProperty("replayResults").GetProperty("passed").GetBoolean());
            }
            File.AppendAllText(source, "\n");
            var stale = Run("test", "M", "--project", root, "--reuse", graph);
            Assert.Equal(1, stale.Code);
            Assert.Contains("stale build", stale.Err);
            Assert.DoesNotContain("TEST PASSED", stale.Out);
            Build(root);
            File.AppendAllText(Path.Combine(root, "sut.py"), "VALUE = 2\n");
            Assert.Equal(1, Run("explore", "M", "--project", root).Code);
            Build(root);
            contract.Machines["M"].Actions.Add("W.Missing");
            SaveContract();
            Build(root);
            var mismatch = Run("test", "M", "--project", root);
            Assert.Equal(1, mismatch.Code);
            Assert.Contains("action universe mismatch", mismatch.Err);
            contract.Machines["M"].Actions.Remove("W.Missing");
            contract.Machines["M"].Rejections.Add(new() { Id = "reject", Action = "W.Do", ObligationId = "forbidden", ExpectedExceptionType = "System.InvalidOperationException" });
            SaveContract();
            Build(root);
            var missing = Run("generate", "M", "--project", root);
            Assert.Equal(1, missing.Code);
            Assert.Contains("missing proof obligations", missing.Err);
            contract.Machines["M"].Rejections.Clear();
            contract.Budgets.States = 1;
            SaveContract();
            Build(root);
            File.Delete(graph);
            var bounded = Run("explore", "M", "--project", root);
            Assert.Equal(1, bounded.Code);
            Assert.Contains("INCOMPLETE", bounded.Err);
            Assert.False(File.Exists(graph));
            var partial = Path.Combine(root, ".specexplorerkit", "out", "partial.seexpl");
            Assert.Equal(1, Run("explore", "M", "--project", root, "--partial-out", partial).Code);
            Assert.Contains("INCOMPLETE", File.ReadAllText(partial));
        }
        finally { TryDeleteDir(root); }
    }

    private static InvocationBehavior Inv(string target, params string[] args) =>
        new() { Target = target, Args = args.Length == 0 ? null : args.ToList() };

    private static ConstructBehavior ScopeModel(string scope)
    {
        var cb = new ConstructBehavior { Kind = ConstructKind.ModelProgram, Reference = "C" };
        cb.Params["scope"] = scope;
        return cb;
    }

    // ---- command aliases + top-level dispatch (CliHost.Run) -----------------------------

    [Fact]
    public void RunAlias_ExploresAndReplays_LikeTest()
    {
        var (code, _, err) = CliHost.Run("run", "ModelProgram", "--project", _turnstile);
        Assert.True(code == 0, err);
    }

    [Fact]
    public void GenAlias_GeneratesTests_LikeGenerate()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "sekcov_gen_" + Guid.NewGuid().ToString("N"));
        try
        {
            var (code, _, err) = CliHost.Run("gen", "ModelProgram", "--project", _turnstile, "--out", outDir, "--max", "2");
            Assert.True(code == 0, err);
        }
        finally { TryDeleteDir(outDir); }
    }

    [Fact]
    public void Version_And_Z3_And_Usage()
    {
        Assert.Equal(0, CliHost.Run("version").Code);
        Assert.Equal(0, CliHost.Run("z3").Code);
        Assert.Equal(1, CliHost.Run("bogus-command").Code);
        // no args → usage banner, exit 0
        Assert.Equal(0, CliHost.Run().Code);
    }

    // ---- view: formats, stdout, and errors ---------------------------------------------

    [Fact]
    public void View_HtmlAndDot_ToStdout_AndUnknownFormatErrors()
    {
        // Produce a graph to view.
        Assert.Equal(0, CliHost.Run("explore", "ModelProgram", "--project", _turnstile).Code);
        var seexpl = Path.Combine(_turnstile, ".specexplorerkit", "out", "ModelProgram.seexpl");

        Assert.Equal(0, CliHost.Run("view", seexpl, "--format", "html").Code);
        Assert.Equal(0, CliHost.Run("view", seexpl, "--format", "dot").Code);
        Assert.Equal(0, CliHost.Run("view", seexpl, "--format", "mmd").Code);

        var (code, _, err) = CliHost.Run("view", seexpl, "--format", "nonsense");
        Assert.Equal(1, code);
        Assert.Contains("unknown --format", err);
    }

    [Fact]
    public void View_MissingFile_And_NoArgs()
    {
        var missing = Path.Combine(Path.GetTempPath(), "sekcov_missing_" + Guid.NewGuid().ToString("N") + ".seexpl");
        var (code, _, err) = CliHost.Run("view", missing);
        Assert.Equal(1, code);
        Assert.Contains("file not found", err);

        // no args → usage, exit 1
        Assert.Equal(1, CliHost.Run("view").Code);
        // help token → usage, exit 0
        Assert.Equal(0, CliHost.Run("view", "--help").Code);
    }

    // ---- init: writes a fresh template config ------------------------------------------

    [Fact]
    public void Init_FreshDir_WritesTemplate_ThenIdempotent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sekcov_init_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var (code, outText, _) = CliHost.Run("init", "--project", dir);
            Assert.Equal(0, code);
            Assert.Contains("Initialized", outText);
            Assert.True(File.Exists(Path.Combine(dir, ".specexplorerkit", "config.json")));

            // Second run: already initialized.
            var (code2, outText2, _) = CliHost.Run("init", "--project", dir);
            Assert.Equal(0, code2);
            Assert.Contains("already initialized", outText2);
        }
        finally { TryDeleteDir(dir); }
    }

    // ---- test / generate error branches (cord-only temp project, no binding) -----------

    [Fact]
    public void Test_And_Generate_MachineNotFound_And_NoBinding()
    {
        var dir = MakeCordOnlyProject(withBinding: false);
        try
        {
            // machine not found
            var (tc, _, terr) = CliHost.Run("test", "NoSuchMachine", "--project", dir);
            Assert.Equal(1, tc);
            Assert.Contains("not found", terr);

            var (gc, _, gerr) = CliHost.Run("generate", "NoSuchMachine", "--project", dir);
            Assert.Equal(1, gc);
            Assert.Contains("not found", gerr);

            // machine found but no binding configured
            var (tc2, _, terr2) = CliHost.Run("test", "ModelProgram", "--project", dir);
            Assert.Equal(1, tc2);
            Assert.Contains("binding", terr2);

            var (gc2, _, gerr2) = CliHost.Run("generate", "ModelProgram", "--project", dir);
            Assert.Equal(1, gc2);
            Assert.Contains("binding", gerr2);
        }
        finally { TryDeleteDir(dir); }
    }

    [Fact]
    public void Test_And_Generate_And_Explore_HelpAndNoArgs()
    {
        Assert.Equal(1, CliHost.Run("test").Code);
        Assert.Equal(0, CliHost.Run("test", "--help").Code);
        Assert.Equal(1, CliHost.Run("generate").Code);
        Assert.Equal(0, CliHost.Run("generate", "--help").Code);
        Assert.Equal(1, CliHost.Run("explore").Code);
        Assert.Equal(0, CliHost.Run("explore", "--help").Code);
    }

    [Fact]
    public void Explore_MachineNotFound_Errors()
    {
        var (code, _, err) = CliHost.Run("explore", "NoSuchMachine", "--project", _turnstile);
        Assert.Equal(1, code);
        Assert.Contains("not found", err);
    }

    // ---- ResolveModelScope: every container node kind ----------------------------------

    [Fact]
    public void ResolveModelScope_WalksAllContainerKinds()
    {
        var cord = Cord("config C { }\nmachine M() : C { construct model program from C }\n");
        var leaf = ScopeModel("Target.Ns");

        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, leaf));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new SequenceBehavior { Items = { leaf } }));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new ChoiceBehavior { Items = { leaf } }));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new ParallelBehavior { Items = { leaf } }));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new RepetitionBehavior { Inner = leaf }));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new GroupBehavior { Inner = leaf }));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new PreconstraintBehavior { Code = "x", Inner = leaf }));
        var bind = new BindBehavior { Inner = leaf };
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, bind));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new ConstructBehavior { Kind = ConstructKind.TestCases, Target = leaf }));

        // null body and a default (unhandled) node → null
        Assert.Null(SekCli.ResolveModelScope(cord, null));
        Assert.Null(SekCli.ResolveModelScope(cord, Inv("SomeAction")));
        // invocation to a known machine recurses
        Assert.Null(SekCli.ResolveModelScope(cord, Inv("M")));
    }

    // ---- FindTestStrategy: strategy key, target, machine refs, containers ---------------

    [Fact]
    public void FindTestStrategy_ResolvesThroughConstructsAndContainers()
    {
        var cord = Cord(
            "config C { }\n" +
            "machine Base() : C { construct model program from C }\n" +
            "machine Suite() : C { construct test cases where Strategy = \"longtests\" for Base }\n");
        Assert.Equal("longtests", SekCli.FindTestStrategy(cord, "Suite"));
        Assert.Null(SekCli.FindTestStrategy(cord, "Base"));

        // Direct AST: test-cases with a Target (no Strategy) recurses into the target.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var withStrategy = new ConstructBehavior { Kind = ConstructKind.TestCases };
        withStrategy.Params["Strategy"] = "shorttests";
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, withStrategy, seen));

        var tcTarget = new ConstructBehavior { Kind = ConstructKind.TestCases, Target = withStrategy };
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, tcTarget, new HashSet<string>(StringComparer.Ordinal)));

        var otherTarget = new ConstructBehavior { Kind = ConstructKind.AcceptingPaths, Target = withStrategy };
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, otherTarget, new HashSet<string>(StringComparer.Ordinal)));

        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, new ParallelBehavior { Items = { withStrategy } }, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, new SequenceBehavior { Items = { withStrategy } }, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, new ChoiceBehavior { Items = { withStrategy } }, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, new GroupBehavior { Inner = withStrategy }, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, new BindBehavior { Inner = withStrategy }, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Null(SekCli.FindTestStrategyIn(cord, Inv("SomeAction"), new HashSet<string>(StringComparer.Ordinal)));
    }

    // ---- ExtractSlice: both parallel orders, choice, repetition, machine inline ---------

    [Fact]
    public void ExtractSlice_ParallelBothOrders_ChoiceAndRepetition()
    {
        var cord = Cord("config C { }\nmachine M() : C { construct model program from C }\n");
        ConstructBehavior Model() => new() { Kind = ConstructKind.ModelProgram, Reference = "C" };

        // model on the LEFT, scenario on the right
        var p1 = new ParallelBehavior();
        p1.Items.Add(Model());
        p1.Items.Add(Inv("A"));
        var (s1, c1) = SekCli.ExtractSlice(cord, p1);
        Assert.Equal("C", c1);
        Assert.NotNull(s1);

        // model on the RIGHT, scenario on the left
        var p2 = new ParallelBehavior();
        p2.Items.Add(Inv("A"));
        p2.Items.Add(Model());
        var (_, c2) = SekCli.ExtractSlice(cord, p2);
        Assert.Equal("C", c2);

        // both model programs → genuine parallel; neither side yields a slice config
        var p3 = new ParallelBehavior();
        p3.Items.Add(Model());
        p3.Items.Add(Model());
        var (s3, c3) = SekCli.ExtractSlice(cord, p3);
        Assert.Null(c3);
        Assert.IsType<ParallelBehavior>(s3);

        // choice + repetition carry the config through
        var choice = new ChoiceBehavior();
        choice.Items.Add(p1);
        var (_, cc) = SekCli.ExtractSlice(cord, choice);
        Assert.Equal("C", cc);

        var rep = new RepetitionBehavior { Inner = p1, Op = "*" };
        var (sr, cr) = SekCli.ExtractSlice(cord, rep);
        Assert.Equal("C", cr);
        Assert.IsType<RepetitionBehavior>(sr);

        // machine reference inlines
        var (_, cm) = SekCli.ExtractSlice(cord, Inv("M"));
        Assert.Null(cm); // M is a bare model program, not a slice
        // null → empty sequence
        var (sn, cn) = SekCli.ExtractSlice(cord, null);
        Assert.IsType<SequenceBehavior>(sn);
        Assert.Null(cn);
    }

    [Fact]
    public void IsPureModelProgram_And_ResolveSliceItem()
    {
        var cord = Cord("config C { }\nmachine M() : C { construct model program from C }\n");
        Assert.Equal("C", SekCli.IsPureModelProgram(cord, Inv("M")));
        Assert.Null(SekCli.IsPureModelProgram(cord, Inv("SomeAction")));

        var body = SekCli.ResolveSliceItem(cord, Inv("M"));
        Assert.NotNull(body);
        Assert.NotNull(SekCli.ResolveSliceItem(cord, new GroupBehavior { Inner = Inv("M") }));
        var plain = Inv("SomeAction");
        Assert.Same(plain, SekCli.ResolveSliceItem(cord, plain));
    }

    // ---- The recursive transforms over the rarer node kinds ----------------------------

    [Fact]
    public void ExpandParamMachines_CoversAllNodeKinds()
    {
        var cord = Cord(
            "config C { }\n" +
            "machine P(int id) : C { A(id) }\n");
        // param-machine expansion substitutes the argument into the invocation's args
        var expanded = SekCli.ExpandParamMachines(cord, Inv("P", "5"));
        Assert.Equal("5", ArgOf(expanded));

        // each container kind round-trips through expansion
        Assert.IsType<PermutationBehavior>(SekCli.ExpandParamMachines(cord, new PermutationBehavior { Items = { Inv("A") } }));
        Assert.IsType<LooseSequenceBehavior>(SekCli.ExpandParamMachines(cord, new LooseSequenceBehavior { Items = { Inv("A") } }));
        Assert.IsType<FailBehavior>(SekCli.ExpandParamMachines(cord, new FailBehavior { Inner = Inv("A") }));
        Assert.IsType<PreconstraintBehavior>(SekCli.ExpandParamMachines(cord, new PreconstraintBehavior { Code = "x", Inner = Inv("A") }));
        Assert.IsType<RepetitionBehavior>(SekCli.ExpandParamMachines(cord, new RepetitionBehavior { Inner = Inv("A") }));
        Assert.IsType<GroupBehavior>(SekCli.ExpandParamMachines(cord, new GroupBehavior { Inner = Inv("A") }));
        Assert.IsType<SequenceBehavior>(SekCli.ExpandParamMachines(cord, new SequenceBehavior { Items = { Inv("A") } }));
        Assert.IsType<ChoiceBehavior>(SekCli.ExpandParamMachines(cord, new ChoiceBehavior { Items = { Inv("A") } }));
        Assert.IsType<ParallelBehavior>(SekCli.ExpandParamMachines(cord, new ParallelBehavior { Items = { Inv("A") } }));
    }

    [Fact]
    public void ReferencesVars_CoversAllNodeKinds()
    {
        var vars = new HashSet<string>(StringComparer.Ordinal) { "id" };
        Assert.True(SekCli.ReferencesVars(Inv("A", "id"), vars));
        Assert.False(SekCli.ReferencesVars(Inv("A", "other"), vars));
        Assert.True(SekCli.ReferencesVars(new PermutationBehavior { Items = { Inv("A", "id") } }, vars));
        Assert.True(SekCli.ReferencesVars(new LooseSequenceBehavior { Items = { Inv("A", "id") } }, vars));
        Assert.True(SekCli.ReferencesVars(new FailBehavior { Inner = Inv("A", "id") }, vars));
        Assert.True(SekCli.ReferencesVars(new PreconstraintBehavior { Code = "x", Inner = Inv("A", "id") }, vars));
        Assert.True(SekCli.ReferencesVars(new RepetitionBehavior { Inner = Inv("A", "id") }, vars));
        Assert.True(SekCli.ReferencesVars(new GroupBehavior { Inner = Inv("A", "id") }, vars));
        Assert.True(SekCli.ReferencesVars(new SequenceBehavior { Items = { Inv("A", "id") } }, vars));
        Assert.True(SekCli.ReferencesVars(new ChoiceBehavior { Items = { Inv("A", "id") } }, vars));
        Assert.True(SekCli.ReferencesVars(new ParallelBehavior { Items = { Inv("A", "id") } }, vars));
        Assert.False(SekCli.ReferencesVars(ScopeModel("X"), vars)); // default arm
    }

    [Fact]
    public void CloneWithSubst_CoversAllNodeKinds_AndSubstitutes()
    {
        var subst = new Dictionary<string, string>(StringComparer.Ordinal) { ["id"] = "9" };
        var cloned = SekCli.CloneWithSubst(Inv("A", "id"), subst);
        Assert.Equal("9", ArgOf(cloned));

        Assert.IsType<PermutationBehavior>(SekCli.CloneWithSubst(new PermutationBehavior { Items = { Inv("A", "id") } }, subst));
        Assert.IsType<LooseSequenceBehavior>(SekCli.CloneWithSubst(new LooseSequenceBehavior { Items = { Inv("A", "id") } }, subst));
        Assert.IsType<FailBehavior>(SekCli.CloneWithSubst(new FailBehavior { Inner = Inv("A", "id") }, subst));
        Assert.IsType<PreconstraintBehavior>(SekCli.CloneWithSubst(new PreconstraintBehavior { Code = "x", Inner = Inv("A", "id") }, subst));
        Assert.IsType<RepetitionBehavior>(SekCli.CloneWithSubst(new RepetitionBehavior { Inner = Inv("A", "id") }, subst));
        Assert.IsType<GroupBehavior>(SekCli.CloneWithSubst(new GroupBehavior { Inner = Inv("A", "id") }, subst));
        Assert.IsType<SequenceBehavior>(SekCli.CloneWithSubst(new SequenceBehavior { Items = { Inv("A", "id") } }, subst));
        Assert.IsType<ChoiceBehavior>(SekCli.CloneWithSubst(new ChoiceBehavior { Items = { Inv("A", "id") } }, subst));
        Assert.IsType<ParallelBehavior>(SekCli.CloneWithSubst(new ParallelBehavior { Items = { Inv("A", "id") } }, subst));
    }

    [Fact]
    public void DesugarLet_CoversContainersAndBind()
    {
        // Container kinds without a let pass through preserving kind.
        Assert.IsType<PermutationBehavior>(SekCli.DesugarLet(new PermutationBehavior { Items = { Inv("A") } }));
        Assert.IsType<LooseSequenceBehavior>(SekCli.DesugarLet(new LooseSequenceBehavior { Items = { Inv("A") } }));
        Assert.IsType<FailBehavior>(SekCli.DesugarLet(new FailBehavior { Inner = Inv("A") }));
        Assert.IsType<PreconstraintBehavior>(SekCli.DesugarLet(new PreconstraintBehavior { Code = "x", Inner = Inv("A") }));
        Assert.IsType<RepetitionBehavior>(SekCli.DesugarLet(new RepetitionBehavior { Inner = Inv("A") }));
        Assert.IsType<GroupBehavior>(SekCli.DesugarLet(new GroupBehavior { Inner = Inv("A") }));
        Assert.IsType<BindBehavior>(SekCli.DesugarLet(new BindBehavior { Inner = Inv("A") }));
        Assert.IsType<SequenceBehavior>(SekCli.DesugarLet(new SequenceBehavior { Items = { Inv("A") } }));
        Assert.IsType<ChoiceBehavior>(SekCli.DesugarLet(new ChoiceBehavior { Items = { Inv("A") } }));
        Assert.IsType<ParallelBehavior>(SekCli.DesugarLet(new ParallelBehavior { Items = { Inv("A") } }));
    }

    [Fact]
    public void KindOf_MapsTypeNames()
    {
        Assert.Equal(ValueKind.String, SekCli.KindOf("string"));
        Assert.Equal(ValueKind.Bool, SekCli.KindOf("bool"));
        Assert.Equal(ValueKind.Long, SekCli.KindOf("long"));
        Assert.Equal(ValueKind.Long, SekCli.KindOf("ulong"));
        Assert.Equal(ValueKind.Int, SekCli.KindOf("int"));
        Assert.Equal(ValueKind.Int, SekCli.KindOf("byte"));
    }

    [Fact]
    public void Unwrap_PeelsNestedGroups()
    {
        var inner = Inv("A");
        var nested = new GroupBehavior { Inner = new GroupBehavior { Inner = inner } };
        Assert.Same(inner, SekCli.Unwrap(nested));
        Assert.Null(SekCli.Unwrap(null));
    }

    // ---- BuildGoalPredicate: property, field, method, missing member -------------------

    [Fact]
    public void BuildGoalPredicate_ReadsBoolMembers()
    {
        var intro = new ModelIntrospector(typeof(GoalModel));

        var byProp = SekCli.BuildGoalPredicate(intro, "Reached");
        Assert.NotNull(byProp);
        Assert.True(byProp!(new GoalModel { Reached = true }));
        Assert.False(byProp!(new GoalModel { Reached = false }));

        // dotted member name → trailing identifier is used
        var dotted = SekCli.BuildGoalPredicate(intro, "GoalModel.Reached");
        Assert.NotNull(dotted);

        // method predicate
        var byMethod = SekCli.BuildGoalPredicate(intro, "IsDone");
        Assert.NotNull(byMethod);
        Assert.True(byMethod!(new GoalModel { Reached = true }));

        // missing member → null; empty / inline → null
        Assert.Null(SekCli.BuildGoalPredicate(intro, "NoSuchMember"));
        Assert.Null(SekCli.BuildGoalPredicate(intro, "(inline)"));
        Assert.Null(SekCli.BuildGoalPredicate(intro, "  "));
    }

    // ---- AnalyzeCord: warning path and error path --------------------------------------

    [Fact]
    public void AnalyzeCord_UnknownBaseConfig_WarnsButSucceeds()
    {
        // SEM003: a config inheriting from an unknown base is a warning (printed), not an error.
        var cord = Cord("config C : Missing { }\nmachine M() : C { construct model program from C }\n");
        var model = SekCli.AnalyzeCord(cord, null);
        Assert.False(model.HasErrors);
    }

    [Fact]
    public void AnalyzeCord_DuplicateMachine_ThrowsOnError()
    {
        // SEM002: a machine declared twice is an error → AnalyzeCord surfaces it as an exception.
        var cord = Cord(
            "config C { }\n" +
            "machine M() : C { construct model program from C }\n" +
            "machine M() : C { construct model program from C }\n");
        var ex = Assert.Throws<InvalidOperationException>(() => SekCli.AnalyzeCord(cord, null));
        Assert.Contains("semantic analysis failed", ex.Message);
    }

    [Fact]
    public void AnalyzeCord_UnknownTargetMachine_ThrowsOnError()
    {
        var cord = Cord("config C { }\nmachine M() : C { construct model program from C }\n");
        Assert.Throws<InvalidOperationException>(() => SekCli.AnalyzeCord(cord, "NoSuchMachine"));
    }

    // ---- helpers -----------------------------------------------------------------------

    private static string Flatten(Behavior b) => string.Join(",", b.ReferencedTargets());

    private static string? ArgOf(Behavior b) => (b as InvocationBehavior)?.Args?.FirstOrDefault();

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
    }

    private static string MakeCordOnlyProject(bool withBinding)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sekcov_proj_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, ".specexplorerkit", "out"));
        Directory.CreateDirectory(Path.Combine(dir, "Model"));
        var binding = withBinding
            ? ",\n  \"binding\": { \"assembly\": \"Sut/bin/Debug/net10.0/X.dll\", \"namespace\": \"X\" }"
            : string.Empty;
        File.WriteAllText(Path.Combine(dir, ".specexplorerkit", "config.json"),
            "{\n  \"model\": { \"assembly\": \"Model/bin/Debug/net10.0/X.dll\", \"type\": \"X.Model\" },\n" +
            "  \"cord\": \"Model\"" + binding + ",\n  \"out\": \".specexplorerkit/out\"\n}");
        File.WriteAllText(Path.Combine(dir, "Model", "Config.cord"),
            "config C { }\nmachine ModelProgram() : C { construct model program from C }\n");
        return dir;
    }

    public sealed class GoalModel : ModelProgram
    {
        public bool Reached { get; set; }

        [Rule("GoalModel.Step")]
        public void Step() => Reached = true;

        public bool IsDone() => Reached;
    }
}
