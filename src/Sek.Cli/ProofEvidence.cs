using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Sek.Core.Model;
using Sek.Core.Seexpl;
using Sek.Engine;

namespace Sek.Cli;

public sealed class ProofContract
{
    public string SchemaVersion { get; set; } = "";
    public Dictionary<string, ProofMachine> Machines { get; set; } = new(StringComparer.Ordinal);
    public List<ProofBuild> Builds { get; set; } = new();
    public List<string> Inputs { get; set; } = new();
    public ProofBudgets Budgets { get; set; } = new();
}

public sealed class ProofMachine
{
    public List<string> Actions { get; set; } = new();
    public List<string> RequiredActions { get; set; } = new();
    public List<ProofCase> PositiveCases { get; set; } = new();
    public List<ProofCase> Rejections { get; set; } = new();
}

public sealed class ProofCase
{
    public string Id { get; set; } = "";
    public string Action { get; set; } = "";
    public List<string>? Arguments { get; set; }
    public Dictionary<string, JsonElement>? FromState { get; set; }
    public string? ObligationId { get; set; }
    public string? ExpectedExceptionType { get; set; }
    public string? ExpectedMessageContains { get; set; }
}

public sealed class ProofBuild
{
    public string Project { get; set; } = "";
    public List<string> Inputs { get; set; } = new();
}

public sealed class ProofBudgets
{
    public int States { get; set; } = 5000;
    public int Transitions { get; set; } = 20000;
    public int Negatives { get; set; } = 5000;
    public long Candidates { get; set; } = 250000;
    public double ExplorationSeconds { get; set; } = 120;
    public double PhaseSeconds { get; set; } = 300;
    public long GraphBytes { get; set; } = 26214400;

    public void Apply(ExplorationOptions options)
    {
        options.MaxStates = Math.Min(options.MaxStates, States);
        options.MaxTransitions = Math.Min(options.MaxTransitions, Transitions);
        options.MaxNegativeTransitions = Negatives;
        options.MaxCandidateEvaluations = Candidates;
        options.MaxElapsedTime = TimeSpan.FromSeconds(ExplorationSeconds);
    }
}

public sealed class ProofSelection
{
    public List<TestGen.TestPath> Paths { get; } = new();
    public List<NegativeTransition> Negatives { get; } = new();
    public List<string> CoveredPositiveIds { get; } = new();
    public List<string> MissingPositiveIds { get; } = new();
    public List<string> CoveredRejectionIds { get; } = new();
    public List<string> MissingRejectionIds { get; } = new();
    public bool Complete => MissingPositiveIds.Count == 0 && MissingRejectionIds.Count == 0;
}

public sealed class ProofEvidence
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly ProjectConfig config;
    private readonly string root;
    private readonly string proofPath;
    private readonly HashSet<string> excluded = new(StringComparer.OrdinalIgnoreCase);
    public ProofContract Contract { get; }
    public string Fingerprint { get; private set; } = "";
    public ProofSelection? Selection { get; set; }
    public Dictionary<string, string> Artifacts { get; } = new(StringComparer.Ordinal);
    public bool Reused { get; private set; }

    public ProofEvidence(ProjectConfig config, string root, params string[] outputPaths)
    {
        this.config = config;
        this.root = Path.GetFullPath(root);
        proofPath = Relative(config.Proof ?? throw new InvalidDataException("strict proof requires config.proof"));
        using var document = JsonDocument.Parse(File.ReadAllText(proofPath));
        CheckDuplicateKeys(document.RootElement);
        Contract = document.Deserialize<ProofContract>(Json) ?? throw new InvalidDataException("invalid proof contract");
        excluded.Add(config.ResolveOutDir(this.root));
        foreach (var output in outputPaths) excluded.Add(Path.GetFullPath(output));
        ValidateContract();
    }

    private static void CheckDuplicateKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException($"ambiguous duplicate proof field '{property.Name}'");
                CheckDuplicateKeys(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) CheckDuplicateKeys(item);
    }

    private void ValidateContract()
    {
        if (Contract.SchemaVersion != "1.0" || Contract.Machines.Count == 0 || Contract.Builds.Count == 0)
            throw new InvalidDataException("proof requires schemaVersion 1.0, exact machines and nonempty builds");
        var budgets = Contract.Budgets;
        if (budgets.States is <= 0 or > 5000 || budgets.Transitions is <= 0 or > 20000
            || budgets.Negatives is <= 0 or > 5000 || budgets.Candidates is <= 0 or > 250000
            || !double.IsFinite(budgets.ExplorationSeconds) || budgets.ExplorationSeconds is <= 0 or > 120
            || !double.IsFinite(budgets.PhaseSeconds) || budgets.PhaseSeconds is <= 0 or > 300
            || budgets.GraphBytes is <= 0 or > 26214400)
            throw new InvalidDataException("proof budgets must be positive and within the supported safety ceilings");
        foreach (var (name, machine) in Contract.Machines)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("missing exact machine scope");
            ExactSet(machine.Actions, "actions");
            ExactSet(machine.RequiredActions, "requiredActions", allowEmpty: true);
            if (machine.RequiredActions.Except(machine.Actions, StringComparer.Ordinal).Any())
                throw new InvalidDataException($"requiredActions outside action universe for '{name}'");
            ExactSet(machine.PositiveCases.Concat(machine.Rejections).Select(item => item.Id).ToList(), "case ids");
            foreach (var item in machine.PositiveCases.Concat(machine.Rejections))
                if (!machine.Actions.Contains(item.Action, StringComparer.Ordinal)) throw new InvalidDataException($"case '{item.Id}' has undeclared action");
            foreach (var item in machine.Rejections)
                if (string.IsNullOrWhiteSpace(item.ObligationId) || string.IsNullOrWhiteSpace(item.ExpectedExceptionType)
                    || !item.ExpectedExceptionType.Contains('.'))
                    throw new InvalidDataException($"rejection '{item.Id}' requires obligationId and fully qualified expectedExceptionType");
        }
        ExactSet(Contract.Builds.Select(build => build.Project).ToList(), "build projects");
        foreach (var build in Contract.Builds)
            if (!build.Project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || build.Inputs.Count == 0)
                throw new InvalidDataException("each build requires a relative csproj and explicit closure inputs");
    }

    private static void ExactSet(List<string> values, string name, bool allowEmpty = false)
    {
        if ((!allowEmpty && values.Count == 0) || values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            throw new InvalidDataException($"missing or ambiguous {name}");
    }

    public ProofMachine Machine(string machine) => Contract.Machines.TryGetValue(machine, out var contract)
        ? contract : throw new InvalidDataException($"missing exact proof scope for machine '{machine}'");

    public void VerifyActions(string machine, IEnumerable<string> actions)
    {
        if (!Machine(machine).Actions.ToHashSet(StringComparer.Ordinal).SetEquals(actions))
            throw new InvalidDataException($"exact action universe mismatch for '{machine}'");
    }

    private string Relative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) throw new InvalidDataException($"expected relative proof input: '{path}'");
        var full = Path.GetFullPath(Path.Combine(root, path));
        return full;
    }

    private bool IsExcluded(string path) => excluded.Any(output => path.Equals(output, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        || Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part is "bin" or "obj" or ".git" or "TestResults" or "BindingAssets" or "generated" or "GeneratedTests");

    private SortedDictionary<string, string> Sources()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { proofPath, Path.Combine(root, ".specexplorerkit", "config.json") };
        void AddInput(string path)
        {
            if (File.Exists(path)) { if (!IsExcluded(path)) files.Add(path); return; }
            if (!Directory.Exists(path)) throw new FileNotFoundException($"proof input missing: {path}");
            void Walk(string directory)
            {
                if (IsExcluded(directory)) return;
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"ambiguous symlink input: {directory}");
                foreach (var file in Directory.EnumerateFiles(directory)) if (!IsExcluded(file)) files.Add(file);
                foreach (var child in Directory.EnumerateDirectories(directory)) Walk(child);
            }
            Walk(path);
        }
        foreach (var project in Projects()) AddInput(Path.GetDirectoryName(project)!);
        AddInput(config.ResolveCordDir(root));
        foreach (var input in Contract.Inputs.Concat(Contract.Builds.SelectMany(build => build.Inputs))) AddInput(Relative(input));
        foreach (var directory in files.Select(Path.GetDirectoryName).Distinct().ToList())
            for (var current = directory; current is not null; current = Path.GetDirectoryName(current))
                foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "NuGet.Config", "nuget.config", "packages.lock.json" })
                {
                    var file = Path.Combine(current, name);
                    if (File.Exists(file)) files.Add(file);
                }
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files.Order(StringComparer.Ordinal)) hashes[Path.GetRelativePath(root, file).Replace('\\', '/')] = Hash(file);
        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory).Where(file => file.EndsWith(".dll") || file.EndsWith(".json")))
            hashes["tool/" + Path.GetFileName(file)] = Hash(file);
        hashes["runtime"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        hashes["sdk"] = Execute("dotnet", new[] { "--version" }, Contract.Budgets.PhaseSeconds).Trim();
        return hashes;
    }

    private List<string> Projects()
    {
        var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string path)
        {
            if (!projects.Add(path)) return;
            if (!File.Exists(path)) throw new FileNotFoundException($"build project missing: {path}");
            var xml = XDocument.Load(path);
            foreach (var element in xml.Descendants().Where(element => element.Name.LocalName == "ProjectReference"))
            {
                var include = element.Attribute("Include")?.Value ?? "";
                if (include.Length == 0 || include.Contains('$') || include.Contains('*') || include.Contains(';'))
                    throw new InvalidDataException($"ambiguous project reference in {path}; declare a concrete closure");
                var reference = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, include.Replace('\\', Path.DirectorySeparatorChar)));
                Visit(Relative(Path.GetRelativePath(root, reference)));
            }
        }
        foreach (var build in Contract.Builds) Visit(Relative(build.Project));
        return projects.Order(StringComparer.Ordinal).ToList();
    }

    private SortedDictionary<string, string> Binaries()
    {
        var assemblies = new[] { config.ResolveModelAssembly(root), config.Binding is null ? null : Path.GetFullPath(Path.Combine(root, config.Binding.Assembly)) };
        var identities = Projects().Select(project => Execute("dotnet", new[] { "msbuild", project, "-getProperty:TargetPath", "-property:Configuration=Debug", "-nologo" }, Contract.Budgets.PhaseSeconds).Trim())
            .Select(path => Path.GetFullPath(path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var assembly in assemblies.Where(path => path is not null).Cast<string>())
        {
            if (!identities.Contains(assembly)) throw new InvalidDataException($"configured binary has no exact build identity: {assembly}");
            if (!File.Exists(assembly)) throw new FileNotFoundException($"build produced no configured binary: {assembly}");
            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(assembly)!, "*", SearchOption.AllDirectories))
                files[Path.GetRelativePath(root, file).Replace('\\', '/')] = Hash(file);
        }
        return files;
    }

    private string AttestationPath => Path.Combine(config.ResolveOutDir(root), "build-attestation.json");
    private sealed class Attestation
    {
        public string SchemaVersion { get; set; } = "1.0";
        public SortedDictionary<string, string> Sources { get; set; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, string> Binaries { get; set; } = new(StringComparer.Ordinal);
    }

    public void Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AttestationPath)!);
        File.Delete(AttestationPath);
        var before = Sources();
        foreach (var build in Contract.Builds)
            Console.WriteLine(Execute("dotnet", new[] { "build", Relative(build.Project), "-c", "Debug", "--nologo", "-v", "minimal" }, Contract.Budgets.PhaseSeconds));
        var after = Sources();
        if (!Equal(before, after)) throw new InvalidDataException("sources changed during build; attestation refused");
        var binaries = Binaries();
        if (!Equal(after, Sources())) throw new InvalidDataException("sources changed while recording build identities; attestation refused");
        Write(AttestationPath, new Attestation { Sources = after, Binaries = binaries });
        Fingerprint = Digest(new { sources = after, binaries });
    }

    public void VerifyBuild()
    {
        if (!File.Exists(AttestationPath)) throw new InvalidDataException("unattested build; run sek build --project <dir>");
        var attestation = JsonSerializer.Deserialize<Attestation>(File.ReadAllText(AttestationPath), Json)!;
        var sources = Sources();
        var binaries = Binaries();
        if (!Equal(sources, Sources())) throw new InvalidDataException("inputs changed while verifying proof freshness");
        if (attestation.SchemaVersion != "1.0" || !Equal(attestation.Sources, sources) || !Equal(attestation.Binaries, binaries))
        {
            var changed = attestation.Sources.Keys.Union(sources.Keys).Where(key => attestation.Sources.GetValueOrDefault(key) != sources.GetValueOrDefault(key))
                .Concat(attestation.Binaries.Keys.Union(binaries.Keys).Where(key => attestation.Binaries.GetValueOrDefault(key) != binaries.GetValueOrDefault(key)));
            throw new InvalidDataException($"stale build: sources/toolchain/binaries changed ({string.Join(", ", changed.Take(10))}); run sek build --project <dir>");
        }
        var fingerprint = Digest(new { sources, binaries });
        if (Fingerprint.Length > 0 && Fingerprint != fingerprint) throw new InvalidDataException("inputs changed during proof");
        Fingerprint = fingerprint;
        foreach (var artifact in binaries) Artifacts["build:" + artifact.Key] = artifact.Value;
    }

    private static bool Equal(SortedDictionary<string, string> left, SortedDictionary<string, string> right) =>
        left.Count == right.Count && left.All(entry => right.TryGetValue(entry.Key, out var hash) && hash == entry.Value);
    public static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static string Digest(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json))).ToLowerInvariant();

    private string Execute(string executable, IEnumerable<string> arguments, double seconds, bool acceptNonzero = false)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"could not start {executable}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        long outputBytes = 0;
        var outputExceeded = 0;
        async Task<string> ReadBounded(StreamReader reader)
        {
            var text = new System.Text.StringBuilder();
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), timeout.Token)) > 0)
            {
                if (Interlocked.Add(ref outputBytes, System.Text.Encoding.UTF8.GetByteCount(buffer, 0, count)) > 2 * 1024 * 1024)
                {
                    Interlocked.Exchange(ref outputExceeded, 1);
                    timeout.Cancel();
                    throw new OperationCanceledException(timeout.Token);
                }
                text.Append(buffer, 0, count);
            }
            return text.ToString();
        }
        var stdout = ReadBounded(process.StandardOutput);
        var stderr = ReadBounded(process.StandardError);
        try
        {
            process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
            var output = stdout.GetAwaiter().GetResult();
            var error = stderr.GetAwaiter().GetResult();
            if (process.ExitCode != 0 && !acceptNonzero) throw new InvalidOperationException($"{executable} failed ({process.ExitCode}): {output}\n{error}");
            return output;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(5000))
                throw new TimeoutException($"INCOMPLETE {executable} phase: owned child did not exit after termination");
            if (Volatile.Read(ref outputExceeded) != 0)
                throw new InvalidDataException($"INCOMPLETE {executable} phase: combined output exceeds 2097152 bytes (log-limit)");
            throw new TimeoutException($"{executable} phase exceeded {seconds}s");
        }
    }

    public ExplorationResult LoadCheckpoint(string path, string machine)
    {
        var checkpoint = JsonSerializer.Deserialize<Checkpoint>(File.ReadAllText(path + ".proof.json"), Json)!;
        if (new FileInfo(path).Length > Contract.Budgets.GraphBytes || checkpoint.SchemaVersion != "1.0"
            || checkpoint.Machine != machine || checkpoint.InputFingerprint != Fingerprint || checkpoint.GraphHash != Hash(path))
            throw new InvalidDataException("stale or invalid exploration checkpoint");
        var graph = SeexplDocument.Load(path).ToGraph();
        if (graph.Machine != machine || graph.Metadata.GetValueOrDefault("completeness") != "COMPLETE")
            throw new InvalidDataException("checkpoint is not a COMPLETE graph for the exact machine");
        Reused = true;
        Artifacts[Path.GetFullPath(path)] = checkpoint.GraphHash;
        var result = new ExplorationResult { Graph = graph };
        if (graph.Metadata.TryGetValue("proofStateJson", out var snapshots))
            foreach (var entry in JsonSerializer.Deserialize<Dictionary<string, string>>(snapshots)!) result.StateJson[entry.Key] = entry.Value;
        if (graph.Metadata.TryGetValue("proofRequirements", out var requirements))
            result.CapturedRequirements.UnionWith(JsonSerializer.Deserialize<List<string>>(requirements)!);
        return result;
    }

    private sealed class Checkpoint
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string Machine { get; set; } = "";
        public string InputFingerprint { get; set; } = "";
        public string GraphHash { get; set; } = "";
    }

    public void SaveCheckpoint(ExplorationGraph graph, string path, CancellationToken token)
    {
        graph.Metadata["completeness"] = "COMPLETE";
        graph.Metadata["inputFingerprint"] = Fingerprint;
        SeexplDocument.SaveGraph(graph, path, Contract.Budgets.GraphBytes, token);
        VerifyBuild();
        var hash = Hash(path);
        Write(path + ".proof.json", new Checkpoint { Machine = graph.Machine, InputFingerprint = Fingerprint, GraphHash = hash });
        Artifacts[Path.GetFullPath(path)] = hash;
    }

    public ConformanceReport ReplayCheckpoint(string path, string machine)
    {
        var reportPath = Path.Combine(config.ResolveOutDir(root), "replay-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Execute("dotnet", new[] { typeof(SekCli).Assembly.Location, "__proof-replay", machine,
                "--project", root, "--reuse", Path.GetFullPath(path), "--report", reportPath }, Contract.Budgets.PhaseSeconds, acceptNonzero: true);
            using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
            var result = document.RootElement.GetProperty("replayResults");
            if (result.ValueKind == JsonValueKind.Null)
                throw new InvalidDataException(document.RootElement.GetProperty("error").GetString() ?? "isolated replay failed before invocation");
            var report = JsonSerializer.Deserialize<ConformanceReport>(result.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("missing isolated replay result");
            foreach (var failure in result.GetProperty("failures").EnumerateArray()) report.Failures.Add(failure.GetString()!);
            if (document.RootElement.GetProperty("inputFingerprint").GetString() != Fingerprint)
                throw new InvalidDataException("isolated replay fingerprint mismatch");
            return report;
        }
        finally { File.Delete(reportPath); }
    }

    public ProofSelection Select(ExplorationGraph graph, string machine)
    {
        var contract = Machine(machine);
        var selection = new ProofSelection();
        Selection = selection;
        var stateJson = graph.Metadata.TryGetValue("proofStateJson", out var snapshots)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(snapshots)! : new Dictionary<string, string>();
        bool Matches(ProofCase item, string state, ActionInvocation action) => item.Action == action.Name
            && (item.Arguments is null || item.Arguments.SequenceEqual(action.Arguments)) && MatchesState(graph, state, item.FromState, stateJson);
        foreach (var item in contract.PositiveCases)
        {
            var witnesses = graph.Transitions.Where(edge => Matches(item, edge.FromStateId, edge.Action))
                .Select(edge => (Edge: edge, Prefix: TestGen.ConcretePrefixTo(graph, edge.FromStateId), Suffix: TestGen.ConcreteSuffixToAccepting(graph, edge.ToStateId)))
                .Where(candidate => candidate.Prefix is not null && candidate.Suffix is not null)
                .OrderBy(candidate => candidate.Prefix!.Count).ThenBy(candidate => candidate.Suffix!.Count)
                .ThenBy(candidate => TestGen.WitnessKey(candidate.Prefix!.Concat(new[] { candidate.Edge }).Concat(candidate.Suffix!)), StringComparer.Ordinal).ToList();
            if (witnesses.Count == 0) selection.MissingPositiveIds.Add(item.Id);
            else
            {
                var witness = witnesses[0];
                selection.Paths.Add(new TestGen.TestPath(witness.Prefix!.Concat(new[] { witness.Edge }).Concat(witness.Suffix!).ToList()));
                selection.CoveredPositiveIds.Add(item.Id);
            }
        }
        foreach (var item in contract.Rejections)
        {
            var witnesses = graph.NegativeTransitions.Where(edge => Matches(item, edge.FromStateId, edge.Action)
                && Conformance.RejectionField(edge, "ObligationId") == item.ObligationId
                && (Conformance.RejectionField(edge, "ExpectedExceptionType") is not { } expected || expected == item.ExpectedExceptionType)
                && (Conformance.RejectionField(edge, "ExpectedMessageContains") is not { } message || message == item.ExpectedMessageContains))
                .Select(edge => (Edge: edge, Prefix: TestGen.ConcretePrefixTo(graph, edge.FromStateId)))
                .Where(candidate => candidate.Prefix is not null).OrderBy(candidate => candidate.Prefix!.Count)
                .ThenBy(candidate => TestGen.WitnessKey(candidate.Prefix!) + candidate.Edge.FromStateId, StringComparer.Ordinal).ToList();
            if (witnesses.Count == 0) selection.MissingRejectionIds.Add(item.Id);
            else
            {
                var edge = witnesses[0].Edge;
                selection.Negatives.Add(WithExpectation(edge, item));
                selection.CoveredRejectionIds.Add(item.Id);
            }
        }
        var coveredActions = selection.Paths.SelectMany(path => path.Steps).Select(edge => edge.Action.Name)
            .Concat(selection.Negatives.Select(edge => edge.Action.Name)).ToHashSet(StringComparer.Ordinal);
        foreach (var action in contract.RequiredActions.Where(action => !coveredActions.Contains(action))) selection.MissingPositiveIds.Add("action:" + action);
        if (selection.Paths.Count == 0) selection.MissingPositiveIds.Add("accepting-legal-nonzero-result");
        return selection;
    }

    private static NegativeTransition WithExpectation(NegativeTransition edge, ProofCase item)
    {
        return edge with
        {
            ObligationId = item.ObligationId,
            ExpectedExceptionType = item.ExpectedExceptionType,
            ExpectedMessageContains = item.ExpectedMessageContains,
        };
    }

    private static bool MatchesState(ExplorationGraph graph, string state, Dictionary<string, JsonElement>? expected, Dictionary<string, string> snapshots)
    {
        if (expected is null || expected.Count == 0) return true;
        var label = snapshots.GetValueOrDefault(state) ?? graph.FindState(state)?.Label;
        if (label is null) return false;
        try
        {
            using var document = JsonDocument.Parse(label);
            return expected.All(field => document.RootElement.TryGetProperty(field.Key, out var actual) && JsonElement.DeepEquals(actual, field.Value));
        }
        catch (JsonException) { return false; }
    }

    public void Report(string path, string verdict, string machine, ExplorationResult? result, TimeSpan duration,
        ConformanceReport? replay = null, string? error = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        Write(path, new
        {
            schemaVersion = "1.0", artifactType = "sek-proof-report", verdict,
            completeness = result?.IsComplete == true ? "COMPLETE" : "INCOMPLETE", machine,
            inputFingerprint = Fingerprint, artifactDigests = Artifacts,
            budgetCounters = new { states = result?.Graph.States.Count ?? 0, transitions = result?.Graph.Transitions.Count ?? 0,
                negatives = result?.Graph.NegativeTransitions.Count ?? 0,
                candidateEvaluations = long.TryParse(result?.Graph.Metadata.GetValueOrDefault("candidateEvaluations"), out var candidates) ? candidates : 0,
                elapsedMilliseconds = double.TryParse(result?.Graph.Metadata.GetValueOrDefault("elapsedMilliseconds"), System.Globalization.CultureInfo.InvariantCulture, out var elapsed) ? elapsed : 0 },
            durationMilliseconds = duration.TotalMilliseconds,
            coveredPositiveIds = Selection?.CoveredPositiveIds ?? new(), missingPositiveIds = Selection?.MissingPositiveIds ?? Contract.Machines.GetValueOrDefault(machine)?.PositiveCases.Select(item => item.Id).ToList() ?? new(),
            coveredRejectionIds = Selection?.CoveredRejectionIds ?? new(), missingRejectionIds = Selection?.MissingRejectionIds ?? Contract.Machines.GetValueOrDefault(machine)?.Rejections.Select(item => item.Id).ToList() ?? new(),
            replayResults = replay is null ? null : new { replay.Passed, replay.TransitionsReplayed, replay.Succeeded, replay.Failed, replay.NegativeReplayed, replay.NegativeRejected, replay.Failures },
            explorationReused = Reused, replayReused = false, error,
        });
    }

    private static void Write(string path, object value)
    {
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, value, Json);
    }
}