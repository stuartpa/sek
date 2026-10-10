using Sek.Cli;
using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Core.Model;
using Sek.Core.Rendering;
using Sek.Core.Seexpl;
using Sek.Engine;
using SpecExplorerKit.Components.Solving;
using System.Reflection;

// SpecExplorerKit (sek) — CLI entry point.
//
//   init                 scaffold a .specexplorerkit project
//   validate             check the model + cord line up
//   explore <machine>    explore a machine into a .seexpl graph
//   view <file.seexpl>   render an exploration (mermaid/dot/html)
//   test <machine>       explore + replay against the SUT (conformance)
//   version

return Sek.Cli.SekCli.Run(args);

namespace Sek.Cli
{
    public static class SekCli
    {
        private static string Version =>
            typeof(SekCli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion.Split('+')[0]
            ?? typeof(SekCli).Assembly.GetName().Version?.ToString(3)
            ?? "unknown";

        public static int Run(string[] args)
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintUsage();
                return 0;
            }

            try
            {
                return args[0].ToLowerInvariant() switch
                {
                    "version" => CmdVersion(),
                    "z3" => CmdZ3(),
                    "view" => CmdView(args.Skip(1).ToArray()),
                    "init" => CmdInit(args.Skip(1).ToArray()),
                    "validate" => CmdValidate(args.Skip(1).ToArray()),
                    "build" => CmdBuild(args.Skip(1).ToArray()),
                    "__proof-replay" => ExecuteProofCommand("__proof-replay", args.Skip(1).ToArray()),
                    "explore" => CmdExplore(args.Skip(1).ToArray()),
                    "test" => CmdTest(args.Skip(1).ToArray()),
                    "run" => CmdTest(args.Skip(1).ToArray()),
                    "generate" => CmdGenerate(args.Skip(1).ToArray()),
                    "gen" => CmdGenerate(args.Skip(1).ToArray()),
                    _ => Unknown(args[0]),
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"sek: error: {ex.Message}");
                return 1;
            }
        }

        public static bool IsHelp(string a) => a is "-h" or "--help" or "help" or "-?" or "/?";

        public static string? GetOption(string[] args, string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        public static int CmdVersion()
        {
            Console.WriteLine($"sek {Version}");
            return 0;
        }

        public static int CmdZ3()
        {
            Console.WriteLine(SpecExplorerKit.Components.Solving.Z3Probe.SelfTest());
            return 0;
        }

        // --- view ----------------------------------------------------------------------

        public static int CmdView(string[] rest)
        {
            if (rest.Length == 0 || IsHelp(rest[0]))
            {
                Console.WriteLine("Usage: sek view <exploration.seexpl> [--format mermaid|dot|html] [--out <file>]");
                return rest.Length == 0 ? 1 : 0;
            }

            var path = rest[0];
            var format = GetOption(rest, "--format") ?? "mermaid";
            var outPath = GetOption(rest, "--out");

            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"sek: error: file not found: {path}");
                return 1;
            }

            var graph = SeexplDocument.Load(path).ToGraph();
            var rendered = format.ToLowerInvariant() switch
            {
                "mermaid" or "mmd" => MermaidRenderer.Render(graph),
                "dot" => DotRenderer.Render(graph),
                "html" => HtmlRenderer.Render(graph),
                _ => throw new ArgumentException($"unknown --format '{format}' (expected mermaid|dot|html)"),
            };

            if (outPath is null)
            {
                Console.WriteLine(rendered);
            }
            else
            {
                File.WriteAllText(outPath, rendered);
                Console.WriteLine($"Wrote {format} for '{graph.Machine}' ({graph.States.Count} states, {graph.Transitions.Count} transitions) to {outPath}");
            }

            return 0;
        }

        // --- init ----------------------------------------------------------------------

        public static int CmdInit(string[] rest)
        {
            var dir = GetOption(rest, "--project") ?? Directory.GetCurrentDirectory();
            var sekDir = Path.Combine(dir, ".specexplorerkit");
            Directory.CreateDirectory(Path.Combine(sekDir, "out"));
            var configPath = Path.Combine(sekDir, "config.json");
            if (File.Exists(configPath))
            {
                Console.WriteLine($"Project already initialized: {configPath}");
                return 0;
            }

            File.WriteAllText(configPath,
                """
                {
                  "model":   { "assembly": "Model/bin/Debug/net10.0/Model.dll", "type": "MyProject.Model" },
                  "cord":    "Model",
                  "binding": { "assembly": "Adapter/bin/Debug/net10.0/Adapter.dll", "namespace": "Adapter" },
                  "out":     ".specexplorerkit/out"
                }
                """);
            Console.WriteLine($"Initialized SpecExplorerKit project at {configPath}");
            return 0;
        }

        // --- shared: load project, cord, model, bounds ---------------------------------

        public static (ProjectConfig config, string dir, CordDocument cord) LoadProject(string[] rest)
        {
            var (config, dir) = ProjectConfig.Load(GetOption(rest, "--project"));
            var cordDir = config.ResolveCordDir(dir);
            var cord = CordDocument.LoadDirectory(cordDir);
            return (config, dir, cord);
        }

        // Runs the semantic-analysis phase (ARC001, phase 3) over the parsed Cord and returns the checked
        // SemanticModel. Errors abort the run (surfaced by the top-level CLI catch); warnings are printed.
        public static Sek.Cord.Semantics.SemanticModel AnalyzeCord(CordDocument cord, string? machine)
        {
            var model = Sek.Cord.Semantics.SemanticAnalyzer.Analyze(cord, machine);
            foreach (var w in model.Diagnostics.Items.Where(d => d.Severity == Sek.Cord.Semantics.DiagnosticSeverity.Warning))
            {
                Console.Error.WriteLine($"sek: warning: {w}");
            }

            if (model.HasErrors)
            {
                var errors = model.Diagnostics.Items
                    .Where(d => d.Severity == Sek.Cord.Semantics.DiagnosticSeverity.Error)
                    .Select(d => d.ToString());
                throw new InvalidOperationException($"semantic analysis failed:{Environment.NewLine}  - {string.Join($"{Environment.NewLine}  - ", errors)}");
            }

            return model;
        }

        public static ExplorationOptions BoundsFor(CordDocument cord, string machine)
        {
            var options = new ExplorationOptions();
            var switches = cord.ResolveMachineSwitches(machine);
            if (switches.TryGetValue("StateBound", out var sb) && int.TryParse(sb, out var sbi)) options.MaxStates = sbi;
            if (switches.TryGetValue("StepBound", out var stb) && int.TryParse(stb, out var stbi)) options.MaxTransitions = stbi;
            if (switches.TryGetValue("PathDepthBound", out var pd) && int.TryParse(pd, out var pdi)) options.MaxDepth = pdi;
            if (switches.TryGetValue("StopAtError", out var sae) && string.Equals(sae, "true", StringComparison.OrdinalIgnoreCase)) options.StopAtError = true;
            return options;
        }

        public static ExplorationGraph ExploreBehavior(CordDocument cord, string machine, ExplorationOptions? options = null)
        {
            var m = cord.GetMachine(machine);
            if (m?.Body is null)
            {
                throw new InvalidOperationException($"Machine '{machine}' has no behavior body to explore.");
            }

            var alphabet = cord.ResolveMachineDeclaredActions(machine).Keys
                .Select(t => { var i = t.LastIndexOf('.'); return i >= 0 ? t[(i + 1)..] : t; })
                .ToHashSet(StringComparer.Ordinal);

            options ??= BoundsFor(cord, machine);
            options.Budget ??= new ExplorationBudget(options);
            var explorer = CreateBehaviorExplorer(cord, alphabet, options);
            return explorer.Explore(machine, ExpandParamMachines(cord, DesugarLet(m.Body, options.Budget), options.Budget));
        }

        private static BehaviorExplorer CreateBehaviorExplorer(CordDocument cord, IEnumerable<string> alphabet, ExplorationOptions options)
        {
            return new BehaviorExplorer(name => cord.GetMachine(name)?.Body, alphabet, options);
        }

        public static ExplorationResult ExploreMachine(ProjectConfig config, string dir, CordDocument cord, string machine, string solverName,
            ExplorationOptions? options = null)
        {
            // Phase 3 (semantic analysis): resolve names, catch cross-reference errors, get the checked
            // SemanticModel — the single source of truth the rest of this method resolves names through.
            var semantics = AnalyzeCord(cord, machine);
            // Scope-aware model loading: a `construct model program … where scope = "Ns.Sub"` selects
            // the model program whose namespace is the scope; otherwise the project's default type.
            var scope = ResolveModelScope(cord, cord.GetMachine(machine)?.Body);
            var modelType = ModelLoader.LoadModelTypeInScope(config.ResolveModelAssembly(dir), scope, config.Model.Type);
            var introspector = new ModelIntrospector(modelType);
            options ??= BoundsFor(cord, machine);
            options.Budget ??= new ExplorationBudget(options);
            // Consume `action all <Adapter>` / explicit `action` declarations: restrict the model's
            // action universe to the imported actions (resolves to all rules for single-adapter models).
            options.AllowedActionLabels = ActionImportResolver.Resolve(
                semantics.ImportedActionTypes(machine),
                semantics.DeclaredActions(machine).Keys,
                introspector.Rules.Select(r => r.ActionLabel));
            // Tag `action event` actions as observations so exploration/test-gen can distinguish them
            // from controllable calls.
            options.EventActionLabels = semantics.EventActions(machine);
            var binds = new Dictionary<string, List<List<string>>>(StringComparer.Ordinal);
            var result = Interpret(introspector, cord, machine, cord.GetMachine(machine)?.Body, options, solverName, binds);
            result.Graph.Metadata["completeness"] = result.IsComplete ? "COMPLETE" : "INCOMPLETE";
            return result;
        }

        // Finds the model `scope` a machine explores under (the first `construct model program … where
        // scope = "…"` reachable through machine references), or null when there is none.
        public static string? ResolveModelScope(CordDocument cord, Sek.Cord.Ast.Behavior? body)
        {
            body = Unwrap(body);
            switch (body)
            {
                case null:
                    return null;
                case Sek.Cord.Ast.ConstructBehavior { Kind: ConstructKind.ModelProgram } cb:
                    return cb.Params.TryGetValue("scope", out var s) ? s : null;
                case Sek.Cord.Ast.InvocationBehavior inv when (inv.Args is null || inv.Args.Count == 0) && cord.GetMachine(inv.Target) is { } m:
                    return ResolveModelScope(cord, m.Body);
                case Sek.Cord.Ast.ParallelBehavior p:
                    return p.Items.Select(i => ResolveModelScope(cord, i)).FirstOrDefault(x => x is not null);
                case Sek.Cord.Ast.SequenceBehavior sq:
                    return sq.Items.Select(i => ResolveModelScope(cord, i)).FirstOrDefault(x => x is not null);
                case Sek.Cord.Ast.ChoiceBehavior ch:
                    return ch.Items.Select(i => ResolveModelScope(cord, i)).FirstOrDefault(x => x is not null);
                case Sek.Cord.Ast.RepetitionBehavior rep:
                    return ResolveModelScope(cord, rep.Inner);
                case Sek.Cord.Ast.GroupBehavior g:
                    return ResolveModelScope(cord, g.Inner);
                case Sek.Cord.Ast.BindBehavior b:
                    return ResolveModelScope(cord, b.Inner);
                case Sek.Cord.Ast.PreconstraintBehavior pcx:
                    return ResolveModelScope(cord, pcx.Inner);
                case Sek.Cord.Ast.ConstructBehavior cbt when cbt.Target is not null:
                    return ResolveModelScope(cord, cbt.Target);
                default:
                    return null;
            }
        }

        // Finds the test-generation strategy for a machine: the first `construct test cases where
        // Strategy = "…"` reachable through machine references (case-insensitive key), or null.
        public static string? FindTestStrategy(CordDocument cord, string machine) =>
            FindTestStrategyIn(cord, cord.GetMachine(machine)?.Body, new HashSet<string>(StringComparer.Ordinal));

        public static string? FindTestStrategyIn(CordDocument cord, Sek.Cord.Ast.Behavior? body, HashSet<string> seen)
        {
            body = Unwrap(body);
            switch (body)
            {
                case Sek.Cord.Ast.ConstructBehavior { Kind: ConstructKind.TestCases } cb:
                    var key = cb.Params.Keys.FirstOrDefault(k => string.Equals(k, "Strategy", StringComparison.OrdinalIgnoreCase));
                    return key is not null ? cb.Params[key]
                        : cb.Target is not null ? FindTestStrategyIn(cord, cb.Target, seen) : null;
                case Sek.Cord.Ast.ConstructBehavior { Target: not null } cbt:
                    return FindTestStrategyIn(cord, cbt.Target, seen);
                case Sek.Cord.Ast.InvocationBehavior inv when (inv.Args is null || inv.Args.Count == 0) && seen.Add(inv.Target) && cord.GetMachine(inv.Target) is { } m:
                    return FindTestStrategyIn(cord, m.Body, seen);
                case Sek.Cord.Ast.ParallelBehavior p: return p.Items.Select(i => FindTestStrategyIn(cord, i, seen)).FirstOrDefault(x => x is not null);
                case Sek.Cord.Ast.SequenceBehavior sq: return sq.Items.Select(i => FindTestStrategyIn(cord, i, seen)).FirstOrDefault(x => x is not null);
                case Sek.Cord.Ast.ChoiceBehavior ch: return ch.Items.Select(i => FindTestStrategyIn(cord, i, seen)).FirstOrDefault(x => x is not null);
                case Sek.Cord.Ast.GroupBehavior g: return FindTestStrategyIn(cord, g.Inner, seen);
                case Sek.Cord.Ast.BindBehavior b: return FindTestStrategyIn(cord, b.Inner, seen);
                default: return null;
            }
        }

        // Recursively interprets a machine body: `bind`, scenario slicing (`||`), and the construct
        // family (model program, bounded exploration, accepting paths, test cases, point shoot,
        // accept completion, requirement coverage).
        public static ExplorationResult Interpret(
            ModelIntrospector introspector, CordDocument cord, string machine,
            Sek.Cord.Ast.Behavior? body, ExplorationOptions options, string solverName,
            Dictionary<string, List<List<string>>> binds, string? startJson = null)
        {
            body = Unwrap(body);

            if (body is Sek.Cord.Ast.PreconstraintBehavior pc)
            {
                // State slicing: `{. Type.Field = value; .}: M` sets model-level state (a static
                // field/property) before exploring M (e.g. bounding the number of jobs/files).
                ApplyStateSlice(introspector, pc.Code);
                if (pc.Inner is Sek.Cord.Ast.InvocationBehavior invp
                    && (invp.Args is null || invp.Args.Count == 0)
                    && cord.GetMachine(invp.Target) is { } innerMachine)
                {
                    return Interpret(introspector, cord, invp.Target, innerMachine.Body, options, solverName, binds, startJson);
                }

                return Interpret(introspector, cord, machine, pc.Inner, options, solverName, binds, startJson);
            }

            if (body is Sek.Cord.Ast.BindBehavior bind)
            {
                foreach (var c in bind.Binds) binds[c.Action] = c.ArgDomains;
                return Interpret(introspector, cord, machine, bind.Inner, options, solverName, binds, startJson);
            }

            var extracted = ExtractSlice(cord, body);
            if (extracted.Config is not null)
            {
                var cfg = extracted.Config;
                var pg = BuildParamGen(cord, cfg, machine, solverName, binds, introspector);
                var explorer = new Explorer(introspector, options, pg);
                var shortNames = introspector.Rules.Select(r => ShortLabel(r.ActionLabel)).ToHashSet(StringComparer.Ordinal);
                var compiled = CreateBehaviorExplorer(cord, shortNames, options).Compile(ExpandParamMachines(cord, DesugarLet(extracted.Scenario, options.Budget), options.Budget));
                return explorer.ExploreSliced(machine, compiled, ShortLabel);
            }

            if (body is Sek.Cord.Ast.ConstructBehavior cb)
            {
                return InterpretConstruct(introspector, cord, machine, cb, options, solverName, binds, startJson);
            }

            // A pure behavior (a scenario / parametrized `let`) with no model program is explored
            // directly as a behavior automaton over its declared action alphabet.
            var behaviorGraph = ExploreBehavior(cord, machine, options);
            return new ExplorationResult { Graph = behaviorGraph, TerminationReason = options.Budget?.TerminationReason };
        }

        public static ExplorationResult InterpretConstruct(
            ModelIntrospector introspector, CordDocument cord, string machine,
            Sek.Cord.Ast.ConstructBehavior cb, ExplorationOptions options, string solverName,
            Dictionary<string, List<List<string>>> binds, string? startJson = null)
        {
            switch (cb.Kind)
            {
                case ConstructKind.ModelProgram:
                {
                    var pg = BuildParamGen(cord, cb.Reference, machine, solverName, binds, introspector);
                    return new Explorer(introspector, options, pg).Explore(machine, startJson);
                }
                case ConstructKind.BoundedExploration:
                {
                    if (cb.Params.TryGetValue("PathDepth", out var pdv) && int.TryParse(pdv, out var pd)) options.MaxDepth = pd;
                    return InterpretTarget(introspector, cord, machine, cb, options, solverName, binds, startJson);
                }
                case ConstructKind.AcceptingPaths:
                {
                    var r = InterpretTarget(introspector, cord, machine, cb, options, solverName, binds);
                    FilterToAcceptingPaths(r.Graph);
                    return r;
                }
                case ConstructKind.RequirementCoverage:
                {
                    var r = InterpretTarget(introspector, cord, machine, cb, options, solverName, binds);
                    var covered = r.CapturedRequirements.ToList();
                    if (covered.Count > 0)
                    {
                        // Real requirement coverage: report the captured requirement ids, and (when the
                        // machine names them) the required set, which of those were covered/missing, and
                        // whether the MinimumRequirementCount threshold was met.
                        r.Graph.Metadata["requirementsCovered"] = string.Join(", ", covered);
                        r.Graph.Metadata["requirementCount"] = covered.Count.ToString();

                        var toCover = (cb.Params.GetValueOrDefault("RequirementsToCover") ?? string.Empty)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        if (toCover.Count > 0)
                        {
                            var hit = toCover.Where(covered.Contains).ToList();
                            var missing = toCover.Where(x => !covered.Contains(x)).ToList();
                            r.Graph.Metadata["requirementsToCover"] = string.Join(", ", toCover);
                            r.Graph.Metadata["requirementsMissing"] = string.Join(", ", missing);
                            r.Graph.Metadata["requirementsCoverageComplete"] = (missing.Count == 0).ToString();
                        }

                        if (cb.Params.TryGetValue("MinimumRequirementCount", out var minText) && int.TryParse(minText, out var min))
                        {
                            var basis = toCover.Count > 0 ? toCover.Count(covered.Contains) : covered.Count;
                            r.Graph.Metadata["minimumRequirementCount"] = min.ToString();
                            r.Graph.Metadata["minimumRequirementCountMet"] = (basis >= min).ToString();
                        }
                    }
                    else
                    {
                        // No Requirement.Capture calls in the model: fall back to the covered action set.
                        var reqs = r.Graph.Transitions.Select(t => t.Action.Name).Distinct().OrderBy(x => x).ToList();
                        r.Graph.Metadata["requirements"] = string.Join(", ", reqs);
                        r.Graph.Metadata["requirementCount"] = reqs.Count.ToString();
                    }

                    return r;
                }
                case ConstructKind.PointShoot:
                {
                    // Phased steering. Phase 1: explore the `for` target (the Point) to establish launch
                    // states. Phase 2: from each launch state, explore the `Shoot` machine (bounded by
                    // PathDepth) to reach a state satisfying the `with (. expr .)` goal predicate. Phase 3:
                    // from each goal, explore the `Completer` machine to an accepting state. The phases are
                    // stitched by model-state hash and pruned to complete root→goal→accepting paths.
                    var goalPred = BuildGoalPredicate(introspector, cb.Params.GetValueOrDefault("with"));
                    var pathDepth = cb.Params.TryGetValue("PathDepth", out var psd) && int.TryParse(psd, out var pdp) ? pdp : (int?)null;
                    var shootName = cb.Params.GetValueOrDefault("Shoot");
                    var completerName = cb.Params.GetValueOrDefault("Completer");

                    options.GoalPredicate = goalPred;
                    var point = InterpretTarget(introspector, cord, machine, cb, options, solverName, binds);

                    // No Shoot/Completer machines: degenerate to steering the Point graph to the goal.
                    if ((shootName is null || cord.GetMachine(shootName) is null)
                        && (completerName is null || cord.GetMachine(completerName) is null))
                    {
                        if (goalPred is not null)
                        {
                            var g0 = (point.Graph.Metadata.GetValueOrDefault("goals") ?? string.Empty)
                                .Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                            FilterToReaching(point.Graph, s => g0.Contains(s.Id));
                            point.Graph.Metadata["goalCount"] = g0.Count.ToString();
                        }

                        return point;
                    }

                    var phases = new List<ExplorationResult> { point };
                    var goalHashes = GoalHashes(point);

                    // Phase 2 — Shoot from each launch (Point) state.
                    if (shootName is not null && cord.GetMachine(shootName) is { } shootM)
                    {
                        var shootOpts = BoundsFor(cord, shootName);
                        shootOpts.GoalPredicate = goalPred;
                        if (pathDepth is int d) shootOpts.MaxDepth = d;
                        shootOpts.AllowedActionLabels = options.AllowedActionLabels;
                        shootOpts.EventActionLabels = options.EventActionLabels;
                        ShareBudget(options, shootOpts);
                        foreach (var json in point.StateJson.Values.ToList())
                        {
                            if (options.Budget?.Check() == false) break;
                            var g2 = Interpret(introspector, cord, shootName, shootM.Body, shootOpts, solverName, new Dictionary<string, List<List<string>>>(StringComparer.Ordinal), json);
                            phases.Add(g2);
                            goalHashes.UnionWith(GoalHashes(g2));
                        }
                    }

                    // Phase 3 — Completer from each goal state, to an accepting state.
                    if (completerName is not null && cord.GetMachine(completerName) is { } compM)
                    {
                        var compOpts = BoundsFor(cord, completerName);
                        compOpts.AllowedActionLabels = options.AllowedActionLabels;
                        compOpts.EventActionLabels = options.EventActionLabels;
                        ShareBudget(options, compOpts);
                        var goalJsons = phases.SelectMany(p => p.StateJson.Where(kv => goalHashes.Contains(p.Graph.FindState(kv.Key)?.Hash ?? "")).Select(kv => kv.Value)).Distinct().ToList();
                        foreach (var json in goalJsons)
                        {
                            if (options.Budget?.Check() == false) break;
                            var g3 = Interpret(introspector, cord, completerName, compM.Body, compOpts, solverName, new Dictionary<string, List<List<string>>>(StringComparer.Ordinal), json);
                            phases.Add(g3);
                        }
                    }

                    var merged = MergePhaseGraphs(machine, phases, goalHashes);
                    FilterToGoalThenAccepting(merged, goalHashes);
                    merged.Metadata["goalCount"] = merged.States.Count(s => goalHashes.Contains(s.Hash)).ToString();
                    merged.Metadata["phases"] = phases.Count.ToString();
                    var combined = new ExplorationResult
                    {
                        Graph = merged, HitBound = phases.Any(phase => phase.HitBound),
                        TerminationReason = options.Budget?.TerminationReason ?? phases.Select(phase => phase.TerminationReason).FirstOrDefault(reason => reason is not null),
                    };
                    foreach (var phase in phases)
                    {
                        combined.Diagnostics.AddRange(phase.Diagnostics);
                        combined.CapturedRequirements.UnionWith(phase.CapturedRequirements);
                        foreach (var state in phase.StateJson)
                        {
                            var hash = phase.Graph.FindState(state.Key)?.Hash;
                            var target = merged.States.FirstOrDefault(candidate => candidate.Hash == hash);
                            if (target is not null) combined.StateJson[target.Id] = state.Value;
                        }
                    }
                    merged.Metadata["completeness"] = combined.IsComplete ? "COMPLETE" : "INCOMPLETE";
                    merged.Metadata["terminationReason"] = combined.TerminationReason ?? "exhausted";
                    merged.Metadata["candidateEvaluations"] = (options.Budget?.CandidateEvaluations ?? 0).ToString();
                    merged.Metadata["elapsedMilliseconds"] = (options.Budget?.Elapsed.TotalMilliseconds ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    return combined;
                }
                case ConstructKind.AcceptCompletion:
                {
                    // Completion: keep only paths that can be completed to an accepting state.
                    var r = InterpretTarget(introspector, cord, machine, cb, options, solverName, binds);
                    FilterToReaching(r.Graph, s => s.Accepting);
                    return r;
                }
                default:
                    // TestCases: explore the target.
                    return InterpretTarget(introspector, cord, machine, cb, options, solverName, binds);
            }
        }

        public static ExplorationResult InterpretTarget(
            ModelIntrospector introspector, CordDocument cord, string machine,
            Sek.Cord.Ast.ConstructBehavior cb, ExplorationOptions options, string solverName,
            Dictionary<string, List<List<string>>> binds, string? startJson = null)
        {
            Sek.Cord.Ast.Behavior? target = cb.Target;
            if (target is null && !string.IsNullOrEmpty(cb.Reference))
            {
                var refMachine = cord.GetMachine(cb.Reference);
                target = refMachine?.Body ?? new Sek.Cord.Ast.ConstructBehavior { Kind = ConstructKind.ModelProgram, Reference = cb.Reference };
            }

            return Interpret(introspector, cord, machine, target, options, solverName, binds, startJson);
        }

        private static void ShareBudget(ExplorationOptions source, ExplorationOptions target)
        {
            source.Budget ??= new ExplorationBudget(source);
            target.Budget = source.Budget;
            target.CancellationToken = source.CancellationToken;
            target.TimeProvider = source.TimeProvider;
            target.MaxCandidateEvaluations = source.MaxCandidateEvaluations;
            target.MaxNegativeTransitions = source.MaxNegativeTransitions;
            target.MaxElapsedTime = source.MaxElapsedTime;
        }

        public static Sek.Cord.Ast.Behavior? Unwrap(Sek.Cord.Ast.Behavior? b) =>
            b is Sek.Cord.Ast.GroupBehavior g ? Unwrap(g.Inner) : b;

        // Keep only states that can reach an accepting state (and the transitions among them).
        public static void FilterToAcceptingPaths(ExplorationGraph graph) =>
            Sek.Core.Analysis.GraphAnalysis.FilterToReaching(graph, s => s.Accepting);

        // Keep only states that lie on a path from the initial state to a "target" state (a state
        // matching <paramref name="isTarget"/>). Used by accepting-paths, point-shoot (goal states),
        // and accept-completion (accepting states).
        public static void FilterToReaching(ExplorationGraph graph, Func<Sek.Core.Model.ModelState, bool> isTarget) =>
            Sek.Core.Analysis.GraphAnalysis.FilterToReaching(graph, isTarget);

        // Builds a goal-state predicate from a Cord `with (. expr .)` steering condition. The common
        // form names a boolean state member (`[Ns.]Type.Member` or a bare `Member`), which is read by
        // reflection on the current model instance. Returns null when there is no condition.
        public static Func<object, bool>? BuildGoalPredicate(ModelIntrospector introspector, string? withExpr)
        {
            if (string.IsNullOrWhiteSpace(withExpr) || withExpr == "(inline)") return null;
            var expr = withExpr.Trim();
            // Trailing identifier of a (possibly dotted) member access is the state predicate name.
            var member = expr.Contains('.') ? expr[(expr.LastIndexOf('.') + 1)..].Trim() : expr;
            if (member.Length == 0 || !member.All(c => char.IsLetterOrDigit(c) || c == '_')) return null;

            var type = introspector.ModelType;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var prop = type.GetProperty(member, flags);
            var field = prop is null ? type.GetField(member, flags) : null;
            var method = prop is null && field is null ? type.GetMethod(member, flags, Type.EmptyTypes) : null;
            if (prop is null && field is null && method is null) return null;

            return instance =>
            {
                try
                {
                    object? v = prop is not null ? prop.GetValue(prop.GetGetMethod(true)!.IsStatic ? null : instance)
                        : field is not null ? field.GetValue(field.IsStatic ? null : instance)
                        : method!.Invoke(method.IsStatic ? null : instance, null);
                    return v is bool b && b;
                }
                catch { return false; }
            };
        }

        public static string ShortLabel(string label)
        {
            var i = label.LastIndexOf('.');
            return i >= 0 ? label[(i + 1)..] : label;
        }

        // The canonical state hashes marked as goals in an exploration (from graph.Metadata["goals"],
        // which lists goal state ids).
        public static HashSet<string> GoalHashes(ExplorationResult r)
        {
            var ids = (r.Graph.Metadata.GetValueOrDefault("goals") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            return r.Graph.States.Where(s => ids.Contains(s.Id)).Select(s => s.Hash).ToHashSet(StringComparer.Ordinal);
        }

        // Stitches the point-shoot phase graphs into one graph, unifying states across phases by their
        // canonical model-state hash. Delegates to the pure Sek.Core graph merge.
        public static ExplorationGraph MergePhaseGraphs(string machine, List<ExplorationResult> phases, HashSet<string> goalHashes)
        {
            var rootHash = phases[0].Graph.FindState(phases[0].Graph.InitialStateId ?? "S0")?.Hash;
            return Sek.Core.Analysis.GraphAnalysis.MergeByHash(machine, phases.Select(p => p.Graph), rootHash);
        }

        // Prunes a stitched point-shoot graph to root → goal → accepting paths.
        public static void FilterToGoalThenAccepting(ExplorationGraph graph, HashSet<string> goalHashes) =>
            Sek.Core.Analysis.GraphAnalysis.FilterToGoalThenAccepting(graph, goalHashes);

        // Executes a Cord state-slice preconstraint `{. Left = literal; ... .}` by setting the named
        // static field/property on a type in the model assembly. The left-hand qualifier (e.g.
        // `ModelProgram.` / `Parameters.`) is a namespace hint; the last segment is the member name.
        public static void ApplyStateSlice(ModelIntrospector introspector, string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            var types = introspector.ModelType.Assembly.GetTypes();
            foreach (var stmt in code.Split(';'))
            {
                var s = stmt.Trim();
                var eq = s.IndexOf('=');
                if (eq <= 0) continue;
                var lhs = s[..eq].Trim();
                var rhs = s[(eq + 1)..].Trim();
                var member = lhs.Contains('.') ? lhs[(lhs.LastIndexOf('.') + 1)..] : lhs;

                foreach (var t in types)
                {
                    var f = t.GetField(member, BindingFlags.Public | BindingFlags.Static);
                    if (f is not null) { try { f.SetValue(null, ConvertLiteral(rhs, f.FieldType)); } catch { } }
                    var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Static);
                    if (p is not null && p.CanWrite) { try { p.SetValue(null, ConvertLiteral(rhs, p.PropertyType)); } catch { } }
                }
            }
        }

        public static object? ConvertLiteral(string token, Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            if (token.Length >= 2 && token[0] == '"' && token[^1] == '"') token = token[1..^1];
            if (t == typeof(bool)) return token == "true";
            if (t == typeof(string)) return token;
            if (t.IsEnum) { try { return Enum.Parse(t, token.Contains('.') ? token[(token.LastIndexOf('.') + 1)..] : token, true); } catch { return null; } }
            if (long.TryParse(token, out var l)) return Convert.ChangeType(l, t);
            return token;
        }

        // Distributes `|| <model program>` out of a behavior expression, returning the composed
        // scenario and the (shared) model config it slices against, or config == null when the body is
        // not a slice. Because `||` distributes over the behavior operators when the model program is
        // shared, `setup; (A || M | B || M)` becomes scenario `setup; (A | B)` sliced against `M`, and
        // `X || (Y || M)` becomes `(X || Y) || M`. Pure behavior parallels (neither side a model
        // program) are preserved. Machine references are inlined.
        public static (Sek.Cord.Ast.Behavior Scenario, string? Config) ExtractSlice(CordDocument cord, Sek.Cord.Ast.Behavior? body)
        {
            body = Unwrap(body);
            switch (body)
            {
                case null:
                    return (new Sek.Cord.Ast.SequenceBehavior(), null);

                case Sek.Cord.Ast.InvocationBehavior inv when (inv.Args is null || inv.Args.Count == 0) && cord.GetMachine(inv.Target) is { } m:
                    // A machine reference: inline it (extracting any slice inside).
                    return ExtractSlice(cord, m.Body);

                case Sek.Cord.Ast.ParallelBehavior par when par.Items.Count == 2:
                {
                    var cfg0 = IsPureModelProgram(cord, par.Items[0]);
                    var cfg1 = IsPureModelProgram(cord, par.Items[1]);
                    if (cfg1 is not null && cfg0 is null)
                    {
                        var (s, c) = ExtractSlice(cord, par.Items[0]);
                        return (s, c ?? cfg1);
                    }

                    if (cfg0 is not null && cfg1 is null)
                    {
                        var (s, c) = ExtractSlice(cord, par.Items[1]);
                        return (s, c ?? cfg0);
                    }

                    // Genuine behavior parallel (neither or both are model programs).
                    var (l, lc) = ExtractSlice(cord, par.Items[0]);
                    var (r, rc) = ExtractSlice(cord, par.Items[1]);
                    var np = new Sek.Cord.Ast.ParallelBehavior { Op = par.Op };
                    np.Items.Add(l);
                    np.Items.Add(r);
                    return (np, lc ?? rc);
                }

                case Sek.Cord.Ast.SequenceBehavior seq:
                {
                    var n = new Sek.Cord.Ast.SequenceBehavior();
                    string? cfg = null;
                    foreach (var it in seq.Items) { var (s, c) = ExtractSlice(cord, it); n.Items.Add(s); cfg ??= c; }
                    return (n, cfg);
                }

                case Sek.Cord.Ast.ChoiceBehavior ch:
                {
                    var n = new Sek.Cord.Ast.ChoiceBehavior();
                    string? cfg = null;
                    foreach (var it in ch.Items) { var (s, c) = ExtractSlice(cord, it); n.Items.Add(s); cfg ??= c; }
                    return (n, cfg);
                }

                case Sek.Cord.Ast.RepetitionBehavior rep:
                {
                    var (s, c) = ExtractSlice(cord, rep.Inner);
                    return (new Sek.Cord.Ast.RepetitionBehavior { Inner = s, Op = rep.Op, Min = rep.Min, Max = rep.Max }, c);
                }

                case Sek.Cord.Ast.GroupBehavior g:
                    return ExtractSlice(cord, g.Inner);

                default:
                    return (body, null);
            }
        }

        // Returns the model-program config name if `item` (resolving machine references and groups) is
        // a bare `construct model program from Cfg`; otherwise null. A slice machine
        // (`scenario || model`) is NOT a pure model program — it is a scenario for further composition.
        public static string? IsPureModelProgram(CordDocument cord, Sek.Cord.Ast.Behavior? item)
        {
            item = Unwrap(item);
            if (item is Sek.Cord.Ast.InvocationBehavior inv && (inv.Args is null || inv.Args.Count == 0) && cord.GetMachine(inv.Target) is { } m)
            {
                item = Unwrap(m.Body);
            }

            return item is Sek.Cord.Ast.ConstructBehavior { Kind: ConstructKind.ModelProgram } cb && !string.IsNullOrEmpty(cb.Reference)
                ? cb.Reference
                : null;
        }

        public static Sek.Cord.Ast.Behavior? ResolveSliceItem(CordDocument cord, Sek.Cord.Ast.Behavior item)
        {
            if (item is Sek.Cord.Ast.GroupBehavior g) item = g.Inner;
            if (item is Sek.Cord.Ast.InvocationBehavior inv && (inv.Args is null || inv.Args.Count == 0))
            {
                var m = cord.GetMachine(inv.Target);
                if (m?.Body is not null) return m.Body;
            }

            return item;
        }

        // Expands parameterized-machine invocations: `machine AnyRequest(int id) { …(id)… }` used as
        // `AnyRequest(5)` is replaced by the machine's body with its parameters substituted by the
        // call arguments. Runs after DesugarLet so let-bound values are already in the arguments.
        public static Sek.Cord.Ast.Behavior ExpandParamMachines(CordDocument cord, Sek.Cord.Ast.Behavior b, ExplorationBudget? budget = null)
        {
            if (budget?.TryCandidate() == false) throw new InvalidOperationException($"INCOMPLETE behavior expansion: {budget.TerminationReason}");
            switch (b)
            {
                case Sek.Cord.Ast.InvocationBehavior inv when inv.Args is { Count: > 0 } && cord.GetMachine(inv.Target) is { } m && m.Parameters.Count > 0:
                {
                    var subst = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (var i = 0; i < m.Parameters.Count && i < inv.Args.Count; i++) subst[m.Parameters[i].Name] = inv.Args[i];
                    return ExpandParamMachines(cord, CloneWithSubst(m.Body!, subst), budget);
                }
                case Sek.Cord.Ast.SequenceBehavior s:
                { var n = new Sek.Cord.Ast.SequenceBehavior(); n.Items.AddRange(s.Items.Select(i => ExpandParamMachines(cord, i, budget))); return n; }
                case Sek.Cord.Ast.ChoiceBehavior c:
                { var n = new Sek.Cord.Ast.ChoiceBehavior(); n.Items.AddRange(c.Items.Select(i => ExpandParamMachines(cord, i, budget))); return n; }
                case Sek.Cord.Ast.ParallelBehavior p:
                { var n = new Sek.Cord.Ast.ParallelBehavior { Op = p.Op }; n.Items.AddRange(p.Items.Select(i => ExpandParamMachines(cord, i, budget))); return n; }
                case Sek.Cord.Ast.PermutationBehavior pm:
                { var n = new Sek.Cord.Ast.PermutationBehavior(); n.Items.AddRange(pm.Items.Select(i => ExpandParamMachines(cord, i, budget))); return n; }
                case Sek.Cord.Ast.LooseSequenceBehavior ls:
                { var n = new Sek.Cord.Ast.LooseSequenceBehavior(); n.Items.AddRange(ls.Items.Select(i => ExpandParamMachines(cord, i, budget))); return n; }
                case Sek.Cord.Ast.RepetitionBehavior r:
                    return new Sek.Cord.Ast.RepetitionBehavior { Inner = ExpandParamMachines(cord, r.Inner, budget), Op = r.Op, Min = r.Min, Max = r.Max };
                case Sek.Cord.Ast.GroupBehavior g:
                    return new Sek.Cord.Ast.GroupBehavior { Inner = ExpandParamMachines(cord, g.Inner, budget) };
                case Sek.Cord.Ast.PreconstraintBehavior pc:
                    return new Sek.Cord.Ast.PreconstraintBehavior { Code = pc.Code, Inner = ExpandParamMachines(cord, pc.Inner, budget) };
                case Sek.Cord.Ast.FailBehavior fb:
                    return new Sek.Cord.Ast.FailBehavior { Inner = ExpandParamMachines(cord, fb.Inner, budget) };
                default:
                    return b;
            }
        }

        // True if a behavior references any of the given variable names as an invocation argument.
        public static bool ReferencesVars(Sek.Cord.Ast.Behavior b, HashSet<string> vars)
        {
            switch (b)
            {
                case Sek.Cord.Ast.InvocationBehavior inv:
                    return inv.Args is not null && inv.Args.Any(a => vars.Contains(a.Trim()));
                case Sek.Cord.Ast.SequenceBehavior s: return s.Items.Any(i => ReferencesVars(i, vars));
                case Sek.Cord.Ast.ChoiceBehavior c: return c.Items.Any(i => ReferencesVars(i, vars));
                case Sek.Cord.Ast.ParallelBehavior p: return p.Items.Any(i => ReferencesVars(i, vars));
                case Sek.Cord.Ast.PermutationBehavior pm: return pm.Items.Any(i => ReferencesVars(i, vars));
                case Sek.Cord.Ast.LooseSequenceBehavior ls: return ls.Items.Any(i => ReferencesVars(i, vars));
                case Sek.Cord.Ast.RepetitionBehavior r: return ReferencesVars(r.Inner, vars);
                case Sek.Cord.Ast.GroupBehavior g: return ReferencesVars(g.Inner, vars);
                case Sek.Cord.Ast.PreconstraintBehavior pc: return ReferencesVars(pc.Inner, vars);
                case Sek.Cord.Ast.FailBehavior fb: return ReferencesVars(fb.Inner, vars);
                default: return false;
            }
        }

        // Expands `let vars where {Condition.In...} in Behavior` into a choice over each variable
        // assignment, substituting the bound values into the inner behavior's invocation arguments.
        public static Sek.Cord.Ast.Behavior DesugarLet(Sek.Cord.Ast.Behavior b, ExplorationBudget? budget = null)
        {
            if (budget?.TryCandidate() == false) throw new InvalidOperationException($"INCOMPLETE let expansion: {budget.TerminationReason}");
            switch (b)
            {
                case Sek.Cord.Ast.LetBehavior let:
                {
                    var da = new DeclaredAction { WhereCode = let.WhereCode };
                    foreach (var v in let.Vars) da.Parameters.Add(v);
                    var ac = CordConstraintExtractor.Extract(da);
                    var solverParams = let.Vars.Select(v => new SolverParam { Name = v.Name, Kind = KindOf(v.Type) }).ToList();
                    // Use Z3 so `let` variables bounded only by a predicate (e.g. Condition.IsTrue(id >= 1
                    // & id <= 8), with no Condition.In) are enumerated; fall back to the enumerative
                    // solver if Z3 yields nothing.
                    IReadOnlyList<IReadOnlyDictionary<string, object?>> assignments;
                    try { assignments = new Z3Solver().Generate(solverParams, ac.Constraints, ac.Combination, 100000); }
                    catch { assignments = new EnumerativeSolver().Generate(solverParams, ac.Constraints, ac.Combination, 100000); }
                    if (assignments.Count == 0)
                    {
                        assignments = new EnumerativeSolver().Generate(solverParams, ac.Constraints, ac.Combination, 100000);
                    }

                    if (budget is not null && assignments.Count >= 100000)
                    {
                        budget.Stop("let-assignment-limit");
                        throw new InvalidOperationException("INCOMPLETE let expansion: assignment enumeration reached its limit");
                    }
                    var inner = DesugarLet(let.Inner, budget);
                    var choices = new List<Sek.Cord.Ast.Behavior>();
                    foreach (var a in assignments)
                    {
                        if (budget?.TryCandidate() == false) throw new InvalidOperationException($"INCOMPLETE let expansion: {budget.TerminationReason}");
                        var subst = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var kv in a) subst[kv.Key] = kv.Value?.ToString() ?? string.Empty;
                        choices.Add(CloneWithSubst(inner, subst));
                    }

                    if (choices.Count == 0) return inner;
                    if (choices.Count == 1) return choices[0];

                    // Left-factor a leading prefix that does not depend on the bound variables, so
                    // `pre; A(v1) | pre; A(v2) | …` becomes `pre; (A(v1) | A(v2) | …)`. This is essential
                    // when the prefix is `...` (== _*): duplicating it across many combinations otherwise
                    // causes an exponential determinization blow-up.
                    var varNames = let.Vars.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);
                    if (inner is Sek.Cord.Ast.SequenceBehavior seqInner)
                    {
                        var k = 0;
                        while (k < seqInner.Items.Count && !ReferencesVars(seqInner.Items[k], varNames)) k++;
                        if (k > 0 && k < seqInner.Items.Count)
                        {
                            Sek.Cord.Ast.Behavior Tail(Dictionary<string, string> subst)
                            {
                                var t = new Sek.Cord.Ast.SequenceBehavior();
                                for (var i = k; i < seqInner.Items.Count; i++) t.Items.Add(CloneWithSubst(seqInner.Items[i], subst));
                                return t.Items.Count == 1 ? t.Items[0] : t;
                            }

                            var tailChoice = new Sek.Cord.Ast.ChoiceBehavior();
                            foreach (var a in assignments)
                            {
                                if (budget?.Check() == false) throw new InvalidOperationException($"INCOMPLETE let expansion: {budget.TerminationReason}");
                                var subst = new Dictionary<string, string>(StringComparer.Ordinal);
                                foreach (var kv in a) subst[kv.Key] = kv.Value?.ToString() ?? string.Empty;
                                tailChoice.Items.Add(Tail(subst));
                            }

                            var factored = new Sek.Cord.Ast.SequenceBehavior();
                            for (var i = 0; i < k; i++) factored.Items.Add(seqInner.Items[i]);
                            factored.Items.Add(tailChoice);
                            return factored;
                        }
                    }

                    var ch = new Sek.Cord.Ast.ChoiceBehavior();
                    ch.Items.AddRange(choices);
                    return ch;
                }
                case Sek.Cord.Ast.SequenceBehavior s:
                { var n = new Sek.Cord.Ast.SequenceBehavior(); n.Items.AddRange(s.Items.Select(item => DesugarLet(item, budget))); return n; }
                case Sek.Cord.Ast.ChoiceBehavior c:
                { var n = new Sek.Cord.Ast.ChoiceBehavior(); n.Items.AddRange(c.Items.Select(item => DesugarLet(item, budget))); return n; }
                case Sek.Cord.Ast.ParallelBehavior p:
                { var n = new Sek.Cord.Ast.ParallelBehavior { Op = p.Op }; n.Items.AddRange(p.Items.Select(item => DesugarLet(item, budget))); return n; }
                case Sek.Cord.Ast.PermutationBehavior pm:
                { var n = new Sek.Cord.Ast.PermutationBehavior(); n.Items.AddRange(pm.Items.Select(item => DesugarLet(item, budget))); return n; }
                case Sek.Cord.Ast.LooseSequenceBehavior ls:
                { var n = new Sek.Cord.Ast.LooseSequenceBehavior(); n.Items.AddRange(ls.Items.Select(item => DesugarLet(item, budget))); return n; }
                case Sek.Cord.Ast.RepetitionBehavior r:
                    return new Sek.Cord.Ast.RepetitionBehavior { Inner = DesugarLet(r.Inner, budget), Op = r.Op, Min = r.Min, Max = r.Max };
                case Sek.Cord.Ast.GroupBehavior gg:
                    return new Sek.Cord.Ast.GroupBehavior { Inner = DesugarLet(gg.Inner, budget) };
                case Sek.Cord.Ast.PreconstraintBehavior pc:
                    return new Sek.Cord.Ast.PreconstraintBehavior { Code = pc.Code, Inner = DesugarLet(pc.Inner, budget) };
                case Sek.Cord.Ast.FailBehavior fb:
                    return new Sek.Cord.Ast.FailBehavior { Inner = DesugarLet(fb.Inner, budget) };
                case Sek.Cord.Ast.BindBehavior bd:
                { var n = new Sek.Cord.Ast.BindBehavior { Inner = DesugarLet(bd.Inner, budget) }; n.Binds.AddRange(bd.Binds); return n; }
                default:
                    return b;
            }
        }

        // Deep-clones a behavior, replacing invocation argument tokens that name a bound variable
        // with its assigned value.
        public static Sek.Cord.Ast.Behavior CloneWithSubst(Sek.Cord.Ast.Behavior b, Dictionary<string, string> subst)
        {
            switch (b)
            {
                case Sek.Cord.Ast.InvocationBehavior inv:
                {
                    var n = new Sek.Cord.Ast.InvocationBehavior { Target = inv.Target, Negated = inv.Negated, Qualifier = inv.Qualifier };
                    if (inv.Args is not null)
                    {
                        n.Args = inv.Args.Select(a => subst.TryGetValue(a.Trim(), out var v) ? v : a).ToList();
                    }

                    return n;
                }
                case Sek.Cord.Ast.SequenceBehavior s:
                { var n = new Sek.Cord.Ast.SequenceBehavior(); n.Items.AddRange(s.Items.Select(i => CloneWithSubst(i, subst))); return n; }
                case Sek.Cord.Ast.ChoiceBehavior c:
                { var n = new Sek.Cord.Ast.ChoiceBehavior(); n.Items.AddRange(c.Items.Select(i => CloneWithSubst(i, subst))); return n; }
                case Sek.Cord.Ast.ParallelBehavior p:
                { var n = new Sek.Cord.Ast.ParallelBehavior { Op = p.Op }; n.Items.AddRange(p.Items.Select(i => CloneWithSubst(i, subst))); return n; }
                case Sek.Cord.Ast.PermutationBehavior pm:
                { var n = new Sek.Cord.Ast.PermutationBehavior(); n.Items.AddRange(pm.Items.Select(i => CloneWithSubst(i, subst))); return n; }
                case Sek.Cord.Ast.LooseSequenceBehavior ls:
                { var n = new Sek.Cord.Ast.LooseSequenceBehavior(); n.Items.AddRange(ls.Items.Select(i => CloneWithSubst(i, subst))); return n; }
                case Sek.Cord.Ast.RepetitionBehavior r:
                    return new Sek.Cord.Ast.RepetitionBehavior { Inner = CloneWithSubst(r.Inner, subst), Op = r.Op, Min = r.Min, Max = r.Max };
                case Sek.Cord.Ast.GroupBehavior gg:
                    return new Sek.Cord.Ast.GroupBehavior { Inner = CloneWithSubst(gg.Inner, subst) };
                case Sek.Cord.Ast.PreconstraintBehavior pc:
                    return new Sek.Cord.Ast.PreconstraintBehavior { Code = pc.Code, Inner = CloneWithSubst(pc.Inner, subst) };
                case Sek.Cord.Ast.FailBehavior fb:
                    return new Sek.Cord.Ast.FailBehavior { Inner = CloneWithSubst(fb.Inner, subst) };
                default:
                    return b;
            }
        }

        public static ValueKind KindOf(string type)
        {
            var t = type.Trim().ToLowerInvariant();
            return t switch
            {
                "string" => ValueKind.String,
                "bool" => ValueKind.Bool,
                "long" or "ulong" => ValueKind.Long,
                _ => ValueKind.Int,
            };
        }

        public static ParameterGeneration BuildParamGen(
            CordDocument cord, string cfgName, string machine, string solverName,
            Dictionary<string, List<List<string>>> binds, ModelIntrospector introspector)
        {
            var byAction = new Dictionary<string, ActionParamSpec>();

            // Seed for Probability.IsTrue(p) branch selection (Cord `switch RandomSeed`, default 0).
            var randomSeed = 0;
            if (cord.ResolveMachineSwitches(machine).TryGetValue("RandomSeed", out var seedText)
                && int.TryParse(seedText, out var parsedSeed))
            {
                randomSeed = parsedSeed;
            }

            var declared = new Dictionary<string, DeclaredAction>();
            if (!string.IsNullOrEmpty(cfgName))
            {
                foreach (var kv in cord.ResolveDeclaredActions(cfgName)) declared[kv.Key] = kv.Value;
            }
            foreach (var kv in cord.ResolveMachineDeclaredActions(machine))
            {
                // Do not let the slice machine's own base config (often the bare scenario config)
                // overwrite a where-constrained declaration coming from the model-program config.
                if (declared.TryGetValue(kv.Key, out var existing)
                    && !string.IsNullOrWhiteSpace(existing.WhereCode)
                    && string.IsNullOrWhiteSpace(kv.Value.WhereCode))
                {
                    continue;
                }

                declared[kv.Key] = kv.Value;
            }

            foreach (var da in declared.Values)
            {
                var ac = CordConstraintExtractor.Extract(da, randomSeed);
                if (ac.Constraints.Count > 0
                    || ac.Combination.Mode != CombinationSpec.Strategy.AllCombinations
                    || ac.Combination.Isolated.Count > 0
                    || ac.Combination.Seeded.Count > 0
                    || ac.Combination.Expand.Count > 0)
                {
                    byAction[da.Target] = new ActionParamSpec { Constraints = ac.Constraints, Combination = ac.Combination };
                }
            }

            // Apply binds: override the parameter domains of bound actions with the bound values.
            foreach (var (action, argDomains) in binds)
            {
                var rule = introspector.Rules.FirstOrDefault(r =>
                    r.ActionLabel == action || ShortLabel(r.ActionLabel) == ShortLabel(action));
                if (rule is null) continue;

                var constraints = new List<SolverConstraint>();
                for (var i = 0; i < argDomains.Count && i < rule.Parameters.Count; i++)
                {
                    var dom = argDomains[i];
                    if (dom.Count == 1 && dom[0] == "_") continue; // unbound parameter
                    // `instances T` falls back to the default reachable-object domain.
                    if (dom.Any(t => t.StartsWith("instances:", StringComparison.Ordinal))) continue;
                    var p = rule.Parameters[i];

                    // Structured struct bind: `JobInfo(Command={"x","y"}, Time={..})` arrives as
                    // `Field=token` markers; group them into per-field domains (Param = "info.Field").
                    if (dom.Any(t => t.Contains('=')))
                    {
                        foreach (var grp in dom.Where(t => t.Contains('=')).GroupBy(t => t[..t.IndexOf('=')].Trim()))
                        {
                            var values = grp.Select(t => ParseBindValue(t[(t.IndexOf('=') + 1)..], typeof(object))).ToList();
                            constraints.Add(new InConstraint { Param = p.Name + "." + grp.Key, Values = values });
                        }

                        continue;
                    }

                    constraints.Add(new InConstraint { Param = p.Name, Values = dom.Select(tok => ParseBindValue(tok, p.Type)).ToList() });
                }

                if (constraints.Count > 0)
                {
                    byAction[rule.ActionLabel] = new ActionParamSpec { Constraints = constraints, Combination = new CombinationSpec() };
                }
            }

            IParameterSolver solver = solverName.Equals("enum", StringComparison.OrdinalIgnoreCase)
                ? new EnumerativeSolver()
                : new Z3Solver();

            return new ParameterGeneration { Solver = solver, ByAction = byAction };
        }

        public static object? ParseBindValue(string token, Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            if (token.Length >= 2 && token[0] == '"' && token[^1] == '"') token = token[1..^1];
            if (t.IsEnum)
            {
                var name = token.Contains('.') ? token[(token.LastIndexOf('.') + 1)..] : token;
                try { return Enum.Parse(t, name, true); } catch { return token; }
            }

            if (long.TryParse(token, out var l)) return l >= int.MinValue && l <= int.MaxValue ? (int)l : l;
            if (token == "true") return true;
            if (token == "false") return false;
            return token;
        }

        // --- explore -------------------------------------------------------------------

        public static int CmdBuild(string[] rest)
        {
            var (config, dir) = ProjectConfig.Load(GetOption(rest, "--project"));
            var evidence = new ProofEvidence(config, dir);
            evidence.Build();
            Console.WriteLine($"Build attested: {evidence.Fingerprint}");
            return 0;
        }

        private static ExplorationResult AcquireGraph(ProjectConfig config, string dir, CordDocument cord, string machine,
            string[] rest, ProofEvidence? proof, CancellationToken token, out ExplorationOptions options)
        {
            options = BoundsFor(cord, machine);
            proof?.Contract.Budgets.Apply(options);
            options.CancellationToken = token;
            options.Budget = new ExplorationBudget(options);
            var reuse = GetOption(rest, "--reuse") ?? config.Reuse;
            if (reuse is not null)
            {
                if (proof is null) throw new InvalidDataException("graph reuse requires config.proof and an attested build");
                return proof.LoadCheckpoint(Path.GetFullPath(Path.Combine(dir, reuse)), machine);
            }
            if (string.IsNullOrWhiteSpace(config.Model.Type))
            {
                var graph = ExploreBehavior(cord, machine, options);
                return new ExplorationResult { Graph = graph, TerminationReason = options.Budget.TerminationReason };
            }
            return ExploreMachine(config, dir, cord, machine, GetOption(rest, "--solver") ?? "z3", options);
        }

        private static ProofEvidence? OpenProof(ProjectConfig config, string dir, string machine, string[] rest, CordDocument cord)
        {
            if (config.Proof is null) return null;
            var outputs = new[] { GetOption(rest, "--report"), GetOption(rest, "--out"), GetOption(rest, "--partial-out") }.Where(path => path is not null).Cast<string>().ToArray();
            var proof = new ProofEvidence(config, dir, outputs);
            return proof;
        }

        private static void VerifyProof(ProofEvidence proof, ProjectConfig config, string dir, string machine, CordDocument cord)
        {
            proof.Machine(machine);
            proof.VerifyBuild();
            var semantics = AnalyzeCord(cord, machine);
            if (string.IsNullOrWhiteSpace(config.Model.Type))
                proof.VerifyActions(machine, semantics.DeclaredActions(machine).Keys);
            else
            {
                var model = new ModelIntrospector(ModelLoader.LoadModelTypeInScope(config.ResolveModelAssembly(dir), ResolveModelScope(cord, cord.GetMachine(machine)?.Body), config.Model.Type));
                var actions = ActionImportResolver.Resolve(semantics.ImportedActionTypes(machine), semantics.DeclaredActions(machine).Keys, model.Rules.Select(rule => rule.ActionLabel));
                proof.VerifyActions(machine, actions ?? model.Rules.Select(rule => rule.ActionLabel).ToHashSet(StringComparer.Ordinal));
            }
        }

        private static int ExecuteProofCommand(string mode, string[] rest)
        {
            var machine = rest[0];
            var (config, dir, cord) = LoadProject(rest);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ProofEvidence? proof = null;
            ExplorationResult? result = null;
            ConformanceReport? replay = null;
            var reportPath = GetOption(rest, "--report") ?? Path.Combine(config.ResolveOutDir(dir), machine + ".proof-report.json");
            try
            {
                if (cord.GetMachine(machine) is null) throw new InvalidDataException($"machine '{machine}' not found. Available: {string.Join(", ", cord.Script.Machines.Select(item => item.Name))}");
                if (mode != "explore" && config.Binding is null) throw new InvalidDataException("no 'binding' configured; replay and generation require a binding");
                proof = OpenProof(config, dir, machine, rest, cord);
                if (proof is not null) VerifyProof(proof, config, dir, machine, cord);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(proof?.Contract.Budgets.ExplorationSeconds ?? 120));
                result = AcquireGraph(config, dir, cord, machine, rest, proof, deadline.Token, out var options);
                RequireComplete(result, GetOption(rest, "--partial-out"), proof?.Contract.Budgets.GraphBytes ?? 26214400, deadline.Token);
                var graph = result.Graph;
                graph.Metadata["completeness"] = "COMPLETE";
                if (proof is not null && !proof.Reused)
                {
                    graph.Metadata["proofStateJson"] = System.Text.Json.JsonSerializer.Serialize(result.StateJson);
                    graph.Metadata["proofRequirements"] = System.Text.Json.JsonSerializer.Serialize(result.CapturedRequirements);
                }
                var selection = proof?.Select(graph, machine);
                if (selection is { Complete: false }) throw new InvalidDataException($"missing proof obligations: {string.Join(", ", selection.MissingPositiveIds.Concat(selection.MissingRejectionIds))}");
                var checkpointPath = GetOption(rest, "--reuse") ?? config.Reuse;
                checkpointPath = checkpointPath is null ? Path.Combine(config.ResolveOutDir(dir), machine + ".seexpl") : Path.GetFullPath(Path.Combine(dir, checkpointPath));
                if (mode == "explore") checkpointPath = GetOption(rest, "--out") ?? checkpointPath;
                if (mode == "explore" || (proof is not null && !proof.Reused))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(checkpointPath))!);
                    if (proof is not null) proof.SaveCheckpoint(graph, checkpointPath, deadline.Token);
                    else SeexplDocument.SaveGraph(graph, checkpointPath, cancellationToken: deadline.Token);
                    Console.WriteLine($"Wrote {checkpointPath}");
                }
                if (mode == "test" || proof is not null)
                {
                    if (config.Binding is null) throw new InvalidDataException("strict proof requires a binding for concrete replay");
                    using var phase = new CancellationTokenSource(TimeSpan.FromSeconds(proof?.Contract.Budgets.PhaseSeconds ?? 300));
                    replay = proof is not null && mode != "__proof-replay"
                        ? proof.ReplayCheckpoint(checkpointPath, machine)
                        : Conformance.Replay(graph, Path.GetFullPath(Path.Combine(dir, config.Binding.Assembly)), config.Binding.Namespace,
                            selection?.Paths, selection?.Negatives, phase.Token);
                    phase.Token.ThrowIfCancellationRequested();
                    if (!replay.Passed) throw new InvalidDataException($"TEST FAILED: {string.Join("; ", replay.Failures.Take(20))}");
                }
                if (mode == "generate")
                {
                    var maximum = int.TryParse(GetOption(rest, "--max"), out var count) && count > 0 ? count : 50;
                    var strategy = TestGen.ParseStrategy(GetOption(rest, "--strategy") ?? FindTestStrategy(cord, machine));
                    var paths = selection?.Paths ?? TestGen.SelectPaths(graph, maximum, strategy);
                    if (paths.Count == 0) throw new InvalidDataException("no test paths could be derived from the exploration");
                    var output = GetOption(rest, "--out") ?? Path.Combine(config.ResolveOutDir(dir), machine + "Tests");
                    var ns = GetOption(rest, "--namespace") ?? (string.IsNullOrWhiteSpace(config.Binding!.Namespace) ? "SpecExplorerKit.GeneratedTests" : config.Binding.Namespace + ".Tests");
                    using var phase = new CancellationTokenSource(TimeSpan.FromSeconds(proof?.Contract.Budgets.PhaseSeconds ?? 300));
                    var generated = TestGen.EmitXunit(graph, paths, output, ns, Path.GetFullPath(Path.Combine(dir, config.Binding!.Assembly)), config.Binding.Namespace, selection?.Negatives);
                    phase.Token.ThrowIfCancellationRequested();
                    if (proof is not null)
                        foreach (var file in Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)) proof.Artifacts[Path.GetFullPath(file)] = ProofEvidence.Hash(file);
                    Console.WriteLine($"Generated {generated.TestCount} positive xUnit test(s), plus {generated.NegativeTestCount} negative test(s). Wrote {generated.TestFile}");
                }
                proof?.VerifyBuild();
                proof?.Report(reportPath, "PASS", machine, result, watch.Elapsed, replay);
                if (proof is null && GetOption(rest, "--report") is not null) WriteBasicReport(reportPath, "PASS", machine, result, watch.Elapsed, null);
                Console.WriteLine($"Explored '{machine}': {graph.States.Count} states, {graph.Transitions.Count} transitions, {graph.States.Count(state => state.Accepting)} accepting, COMPLETE.");
                if (mode == "test") Console.WriteLine("TEST PASSED");
                return 0;
            }
            catch (Exception error)
            {
                var incomplete = result?.IsComplete == false || error is OperationCanceledException or TimeoutException
                    || error.Message.Contains("INCOMPLETE", StringComparison.Ordinal) || error.Message.Contains(".seexpl serialization exceeds", StringComparison.Ordinal);
                if (incomplete && result is not null)
                {
                    result.Graph.Metadata["completeness"] = "INCOMPLETE";
                    result = new ExplorationResult { Graph = result.Graph, HitBound = result.HitBound, TerminationReason = result.TerminationReason ?? error.Message };
                }
                var verdict = incomplete ? "INCOMPLETE" : "FAIL";
                if (proof is not null) proof.Report(reportPath, verdict, machine, result, watch.Elapsed, replay, error.Message);
                else if (config.Proof is not null || GetOption(rest, "--report") is not null) WriteBasicReport(reportPath, verdict, machine, result, watch.Elapsed, error.Message);
                Console.Error.WriteLine($"sek: error: {error.Message}");
                return 1;
            }
        }

        private static void WriteBasicReport(string path, string verdict, string machine, ExplorationResult? result, TimeSpan duration, string? error)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var stream = File.Create(path);
            System.Text.Json.JsonSerializer.Serialize(stream, new { schemaVersion = "1.0", artifactType = "sek-proof-report", verdict,
                completeness = result?.IsComplete == true ? "COMPLETE" : "INCOMPLETE", machine, inputFingerprint = (string?)null,
                artifactDigests = new Dictionary<string, string>(),
                budgetCounters = new { states = result?.Graph.States.Count ?? 0, transitions = result?.Graph.Transitions.Count ?? 0,
                    negatives = result?.Graph.NegativeTransitions.Count ?? 0, candidateEvaluations = 0, elapsedMilliseconds = duration.TotalMilliseconds },
                coveredPositiveIds = Array.Empty<string>(), missingPositiveIds = Array.Empty<string>(),
                coveredRejectionIds = Array.Empty<string>(), missingRejectionIds = Array.Empty<string>(), replayResults = (object?)null,
                explorationReused = false, replayReused = false, durationMilliseconds = duration.TotalMilliseconds, error }, ProofEvidence.Json);
        }

        public static void RequireComplete(ExplorationResult result, string? partialPath = null,
            long maxBytes = 26214400, CancellationToken cancellationToken = default)
        {
            if (result.IsComplete) return;
            result.Graph.Metadata["completeness"] = "INCOMPLETE";
            if (partialPath is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(partialPath))!);
                SeexplDocument.SaveGraph(result.Graph, partialPath, maxBytes, cancellationToken);
            }
            throw new InvalidOperationException($"INCOMPLETE exploration: {result.TerminationReason}; no proof, replay or generation permitted.");
        }

        public static int CmdExplore(string[] rest)
        {
            if (rest.Length == 0 || IsHelp(rest[0]))
            {
                Console.WriteLine("Usage: sek explore <machine> [--project <dir>] [--out <file>]");
                return rest.Length == 0 ? 1 : 0;
            }

            return ExecuteProofCommand("explore", rest);
        }

        // --- validate ------------------------------------------------------------------

        public static int CmdValidate(string[] rest)
        {
            var (config, dir, cord) = LoadProject(rest);
            var modelType = ModelLoader.LoadModelType(config.ResolveModelAssembly(dir), config.Model.Type);
            var introspector = new ModelIntrospector(modelType);
            var ruleLabels = introspector.Rules.Select(r => r.ActionLabel).ToHashSet();

            // Phase 3 (semantic analysis): the shared SemanticAnalyzer owns the Cord-level cross-reference
            // checks (duplicate declarations, unknown base configs, unknown construct references) and
            // produces a diagnostic bag we extend with the reflection-level rule-mapping check below.
            var semantics = Sek.Cord.Semantics.SemanticAnalyzer.Analyze(cord);
            var diagnostics = semantics.Diagnostics;

            // Reflection-level check (needs the loaded model): every declared cord action maps to a rule.
            foreach (var target in cord.AllDeclaredActionTargets().Distinct())
            {
                if (!ruleLabels.Contains(target))
                {
                    diagnostics.Error("SEM100", $"cord action '{target}' has no matching model rule");
                }
            }

            Console.WriteLine($"Model:   {modelType.FullName}");
            Console.WriteLine($"Rules:   {introspector.Rules.Count} ({string.Join(", ", ruleLabels.OrderBy(x => x))})");
            Console.WriteLine($"Accept:  {introspector.AcceptingConditions.Count} accepting condition(s)");
            Console.WriteLine($"Cord:    {cord.Script.Configurations.Count} config(s), {cord.Script.Machines.Count} machine(s)");

            foreach (var w in diagnostics.Items.Where(d => d.Severity == Sek.Cord.Semantics.DiagnosticSeverity.Warning))
            {
                Console.Error.WriteLine($"  ~ {w}");
            }

            var errors = diagnostics.Items.Where(d => d.Severity == Sek.Cord.Semantics.DiagnosticSeverity.Error).ToList();
            if (errors.Count == 0)
            {
                Console.WriteLine("validate: OK");
                return 0;
            }

            Console.Error.WriteLine($"validate: {errors.Count} problem(s):");
            foreach (var e in errors) Console.Error.WriteLine($"  - {e}");
            return 1;
        }

        // --- test / run (conformance) --------------------------------------------------

        public static int CmdTest(string[] rest)
        {
            if (rest.Length == 0 || IsHelp(rest[0]))
            {
                Console.WriteLine("Usage: sek test <machine> [--project <dir>]");
                return rest.Length == 0 ? 1 : 0;
            }

            return ExecuteProofCommand("test", rest);
        }

        // --- generate (test-case generation) -------------------------------------------

        public static int CmdGenerate(string[] rest)
        {
            if (rest.Length == 0 || IsHelp(rest[0]))
            {
                Console.WriteLine("Usage: sek generate <machine> [--project <dir>] [--out <dir>] [--namespace <ns>] [--max <n>] [--solver z3|enum]");
                return rest.Length == 0 ? 1 : 0;
            }

            return ExecuteProofCommand("generate", rest);
        }

        public static int Unknown(string command)
        {
            Console.Error.WriteLine($"sek: unknown command '{command}'.");
            PrintUsage();
            return 1;
        }

        public static void PrintUsage()
        {
            Console.WriteLine($"""
            SpecExplorerKit (sek) {Version} — modern, CLI-first model-based testing (Spec Explorer + Cord revived).

            Usage:
              sek <command> [options]

            Commands:
              init [--project <dir>]        Scaffold a .specexplorerkit/ project
              validate [--project <dir>]    Check the model program and Cord scripts line up
              build [--project <dir>]       Build and attest the strict proof closure (Debug)
              explore <machine>             Explore a machine into a .seexpl graph
                                              --project <dir>   (default: current dir)
                                              --out <file>      (default: .specexplorerkit/out/<machine>.seexpl)
              view <file.seexpl>            Render an exploration
                                              --format mermaid|dot|html   (default: mermaid)
                                              --out <file>                (default: stdout)
              test <machine>                Explore, then replay against the SUT (conformance)
              generate <machine>            Generate an xUnit test project from the exploration
                                              --out <dir>       (default: .specexplorerkit/out/<machine>Tests)
                                              --namespace <ns>  (default: <binding-namespace>.Tests)
                                              --max <n>         (max test paths; default 50)
              version                       Print version

                        Proof options (config.proof): --report <file>, --reuse <complete.seexpl>
                        Diagnostic export: --partial-out <file> (INCOMPLETE only; command still fails)
            """);
        }
    }
}
