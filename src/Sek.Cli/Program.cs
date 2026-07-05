using Sek.Cli;
using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Core.Model;
using Sek.Core.Rendering;
using Sek.Core.Seexpl;
using Sek.Engine;
using Sek.Solver;

// SpecExplorerKit (sek) — CLI entry point.
//
//   init                 scaffold a .specexplorerkit project
//   validate             check the model + cord line up
//   explore <machine>    explore a machine into a .seexpl graph
//   view <file.seexpl>   render an exploration (mermaid/dot/html)
//   test <machine>       explore + replay against the SUT (conformance)
//   version

const string Version = "0.1.0";

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

static bool IsHelp(string a) => a is "-h" or "--help" or "help" or "-?" or "/?";

static string? GetOption(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

int CmdVersion()
{
    Console.WriteLine($"sek {Version}");
    return 0;
}

int CmdZ3()
{
    Console.WriteLine(Sek.Solver.Z3Probe.SelfTest());
    return 0;
}

// --- view ----------------------------------------------------------------------

int CmdView(string[] rest)
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

int CmdInit(string[] rest)
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
          "model":   { "assembly": "Model/bin/Debug/Model.dll", "type": "MyProject.Model" },
          "cord":    "Model",
          "binding": { "assembly": "Adapter/bin/Debug/Adapter.dll", "namespace": "Adapter" },
          "out":     ".specexplorerkit/out"
        }
        """);
    Console.WriteLine($"Initialized SpecExplorerKit project at {configPath}");
    return 0;
}

// --- shared: load project, cord, model, bounds ---------------------------------

(ProjectConfig config, string dir, CordDocument cord) LoadProject(string[] rest)
{
    var (config, dir) = ProjectConfig.Load(GetOption(rest, "--project"));
    var cordDir = config.ResolveCordDir(dir);
    var cord = CordDocument.LoadDirectory(cordDir);
    return (config, dir, cord);
}

ExplorationOptions BoundsFor(CordDocument cord, string machine)
{
    var options = new ExplorationOptions();
    var switches = cord.ResolveMachineSwitches(machine);
    if (switches.TryGetValue("StateBound", out var sb) && int.TryParse(sb, out var sbi)) options.MaxStates = sbi;
    if (switches.TryGetValue("StepBound", out var stb) && int.TryParse(stb, out var stbi)) options.MaxTransitions = stbi;
    if (switches.TryGetValue("PathDepthBound", out var pd) && int.TryParse(pd, out var pdi)) options.MaxDepth = pdi;
    return options;
}

ExplorationGraph ExploreBehavior(CordDocument cord, string machine)
{
    var m = cord.GetMachine(machine);
    if (m?.Body is null)
    {
        throw new InvalidOperationException($"Machine '{machine}' has no behavior body to explore.");
    }

    var alphabet = cord.ResolveMachineDeclaredActions(machine).Keys
        .Select(t => { var i = t.LastIndexOf('.'); return i >= 0 ? t[(i + 1)..] : t; })
        .ToHashSet(StringComparer.Ordinal);

    var explorer = new BehaviorExplorer(name => cord.GetMachine(name)?.Body, alphabet);
    return explorer.Explore(machine, m.Body);
}

ExplorationResult ExploreMachine(ProjectConfig config, string dir, CordDocument cord, string machine, string solverName)
{
    var modelType = ModelLoader.LoadModelType(config.ResolveModelAssembly(dir), config.Model.Type);
    var introspector = new ModelIntrospector(modelType);
    var options = BoundsFor(cord, machine);
    var binds = new Dictionary<string, List<List<string>>>(StringComparer.Ordinal);
    return Interpret(introspector, cord, machine, cord.GetMachine(machine)?.Body, options, solverName, binds);
}

// Recursively interprets a machine body: `bind`, scenario slicing (`||`), and the construct
// family (model program, bounded exploration, accepting paths, test cases, point shoot,
// accept completion, requirement coverage).
ExplorationResult Interpret(
    ModelIntrospector introspector, CordDocument cord, string machine,
    Sek.Cord.Ast.Behavior? body, ExplorationOptions options, string solverName,
    Dictionary<string, List<List<string>>> binds)
{
    body = Unwrap(body);

    if (body is Sek.Cord.Ast.BindBehavior bind)
    {
        foreach (var c in bind.Binds) binds[c.Action] = c.ArgDomains;
        return Interpret(introspector, cord, machine, bind.Inner, options, solverName, binds);
    }

    var slice = TryGetSliceScenario(cord, body);
    if (slice is not null)
    {
        var cfg = body!.FindConstruct()?.Reference ?? cord.GetMachine(machine)?.BaseConfigs.FirstOrDefault() ?? string.Empty;
        var pg = BuildParamGen(cord, cfg, machine, solverName, binds, introspector);
        var explorer = new Explorer(introspector, options, pg);
        var shortNames = introspector.Rules.Select(r => ShortLabel(r.ActionLabel)).ToHashSet(StringComparer.Ordinal);
        var compiled = new BehaviorExplorer(name => cord.GetMachine(name)?.Body, shortNames).Compile(slice);
        return explorer.ExploreSliced(machine, compiled, ShortLabel);
    }

    if (body is Sek.Cord.Ast.ConstructBehavior cb)
    {
        return InterpretConstruct(introspector, cord, machine, cb, options, solverName, binds);
    }

    var cfgName = cord.GetMachine(machine)?.BaseConfigs.FirstOrDefault() ?? string.Empty;
    var paramGen = BuildParamGen(cord, cfgName, machine, solverName, binds, introspector);
    return new Explorer(introspector, options, paramGen).Explore(machine);
}

ExplorationResult InterpretConstruct(
    ModelIntrospector introspector, CordDocument cord, string machine,
    Sek.Cord.Ast.ConstructBehavior cb, ExplorationOptions options, string solverName,
    Dictionary<string, List<List<string>>> binds)
{
    switch (cb.Kind)
    {
        case ConstructKind.ModelProgram:
        {
            var pg = BuildParamGen(cord, cb.Reference, machine, solverName, binds, introspector);
            return new Explorer(introspector, options, pg).Explore(machine);
        }
        case ConstructKind.BoundedExploration:
        {
            if (cb.Params.TryGetValue("PathDepth", out var pdv) && int.TryParse(pdv, out var pd)) options.MaxDepth = pd;
            return InterpretTarget(introspector, cord, machine, cb, options, solverName, binds);
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
            var reqs = r.Graph.Transitions.Select(t => t.Action.Name).Distinct().OrderBy(x => x).ToList();
            r.Graph.Metadata["requirements"] = string.Join(", ", reqs);
            r.Graph.Metadata["requirementCount"] = reqs.Count.ToString();
            return r;
        }
        default:
            // TestCases, PointShoot, AcceptCompletion: explore the target. (The point-shoot /
            // accept-completion steering heuristics are approximated by exploring the target.)
            return InterpretTarget(introspector, cord, machine, cb, options, solverName, binds);
    }
}

ExplorationResult InterpretTarget(
    ModelIntrospector introspector, CordDocument cord, string machine,
    Sek.Cord.Ast.ConstructBehavior cb, ExplorationOptions options, string solverName,
    Dictionary<string, List<List<string>>> binds)
{
    Sek.Cord.Ast.Behavior? target = cb.Target;
    if (target is null && !string.IsNullOrEmpty(cb.Reference))
    {
        var refMachine = cord.GetMachine(cb.Reference);
        target = refMachine?.Body ?? new Sek.Cord.Ast.ConstructBehavior { Kind = ConstructKind.ModelProgram, Reference = cb.Reference };
    }

    return Interpret(introspector, cord, machine, target, options, solverName, binds);
}

static Sek.Cord.Ast.Behavior? Unwrap(Sek.Cord.Ast.Behavior? b) =>
    b is Sek.Cord.Ast.GroupBehavior g ? Unwrap(g.Inner) : b;

// Keep only states that can reach an accepting state (and the transitions among them).
static void FilterToAcceptingPaths(ExplorationGraph graph)
{
    var canReach = new HashSet<string>(graph.States.Where(s => s.Accepting).Select(s => s.Id));
    var changed = true;
    while (changed)
    {
        changed = false;
        foreach (var t in graph.Transitions)
        {
            if (canReach.Contains(t.ToStateId) && canReach.Add(t.FromStateId)) changed = true;
        }
    }

    graph.Transitions.RemoveAll(t => !canReach.Contains(t.FromStateId) || !canReach.Contains(t.ToStateId));
    graph.States.RemoveAll(s => !canReach.Contains(s.Id) && !s.Initial);
}

static string ShortLabel(string label)
{
    var i = label.LastIndexOf('.');
    return i >= 0 ? label[(i + 1)..] : label;
}

// If the machine body is `A || B` where exactly one side constructs a model program,
// returns the other side (the scenario behavior) for slicing; otherwise null.
static Sek.Cord.Ast.Behavior? TryGetSliceScenario(CordDocument cord, Sek.Cord.Ast.Behavior? body)
{
    body = Unwrap(body);
    if (body is not Sek.Cord.Ast.ParallelBehavior par || par.Items.Count != 2) return null;

    var b0 = ResolveSliceItem(cord, par.Items[0]);
    var b1 = ResolveSliceItem(cord, par.Items[1]);
    var m0 = b0?.FindConstruct()?.Kind == ConstructKind.ModelProgram;
    var m1 = b1?.FindConstruct()?.Kind == ConstructKind.ModelProgram;

    if (m0 && !m1) return b1;
    if (m1 && !m0) return b0;
    return null;
}

static Sek.Cord.Ast.Behavior? ResolveSliceItem(CordDocument cord, Sek.Cord.Ast.Behavior item)
{
    if (item is Sek.Cord.Ast.GroupBehavior g) item = g.Inner;
    if (item is Sek.Cord.Ast.InvocationBehavior inv && (inv.Args is null || inv.Args.Count == 0))
    {
        var m = cord.GetMachine(inv.Target);
        if (m?.Body is not null) return m.Body;
    }

    return item;
}

ParameterGeneration BuildParamGen(
    CordDocument cord, string cfgName, string machine, string solverName,
    Dictionary<string, List<List<string>>> binds, ModelIntrospector introspector)
{
    var byAction = new Dictionary<string, ActionParamSpec>();

    var declared = new Dictionary<string, DeclaredAction>();
    if (!string.IsNullOrEmpty(cfgName))
    {
        foreach (var kv in cord.ResolveDeclaredActions(cfgName)) declared[kv.Key] = kv.Value;
    }

    foreach (var kv in cord.ResolveMachineDeclaredActions(machine)) declared[kv.Key] = kv.Value;

    foreach (var da in declared.Values)
    {
        var ac = CordConstraintExtractor.Extract(da);
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
            var p = rule.Parameters[i];
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

static object? ParseBindValue(string token, Type type)
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

int CmdExplore(string[] rest)
{
    if (rest.Length == 0 || IsHelp(rest[0]))
    {
        Console.WriteLine("Usage: sek explore <machine> [--project <dir>] [--out <file>]");
        return rest.Length == 0 ? 1 : 0;
    }

    var machine = rest[0];
    var solverName = GetOption(rest, "--solver") ?? "z3";
    var (config, dir, cord) = LoadProject(rest);

    var cordMachine = cord.GetMachine(machine);
    if (cordMachine is null)
    {
        Console.Error.WriteLine($"sek: error: machine '{machine}' not found in Cord. Available: {string.Join(", ", cord.Script.Machines.Select(m => m.Name))}");
        return 1;
    }

    // Behavior-mode: a pure Cord scenario/operator machine (no model program).
    if (string.IsNullOrWhiteSpace(config.Model.Type))
    {
        var bgraph = ExploreBehavior(cord, machine);
        var boutPath = GetOption(rest, "--out") ?? Path.Combine(config.ResolveOutDir(dir), machine + ".seexpl");
        Directory.CreateDirectory(Path.GetDirectoryName(boutPath)!);
        SeexplDocument.FromGraph(bgraph).Save(boutPath);
        Console.WriteLine($"Explored behavior '{machine}': {bgraph.States.Count} states, {bgraph.Transitions.Count} transitions, {bgraph.States.Count(s => s.Accepting)} accepting.");
        Console.WriteLine($"Wrote {boutPath}");
        return 0;
    }

    var result = ExploreMachine(config, dir, cord, machine, solverName);
    var graph = result.Graph;

    var outPath = GetOption(rest, "--out") ?? Path.Combine(config.ResolveOutDir(dir), machine + ".seexpl");
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    SeexplDocument.FromGraph(graph).Save(outPath);

    Console.WriteLine($"Explored '{machine}': {graph.States.Count} states, {graph.Transitions.Count} transitions, {graph.States.Count(s => s.Accepting)} accepting{(result.HitBound ? " (bound hit)" : "")}.");
    Console.WriteLine($"Wrote {outPath}");
    if (result.Diagnostics.Count > 0)
    {
        Console.WriteLine($"  {result.Diagnostics.Count} rule diagnostic(s); first: {result.Diagnostics[0]}");
    }

    return 0;
}

// --- validate ------------------------------------------------------------------

int CmdValidate(string[] rest)
{
    var (config, dir, cord) = LoadProject(rest);
    var modelType = ModelLoader.LoadModelType(config.ResolveModelAssembly(dir), config.Model.Type);
    var introspector = new ModelIntrospector(modelType);
    var ruleLabels = introspector.Rules.Select(r => r.ActionLabel).ToHashSet();

    var problems = new List<string>();

    // Every declared cord action should map to a model rule.
    foreach (var target in cord.AllDeclaredActionTargets().Distinct())
    {
        if (!ruleLabels.Contains(target))
        {
            problems.Add($"cord action '{target}' has no matching model rule");
        }
    }

    // Every construct reference should resolve to a config or machine.
    foreach (var m in cord.Script.Machines)
    {
        var construct = m.Body?.FindConstruct();
        if (construct is null) continue;
        var refName = construct.Reference;
        var resolves = cord.GetConfiguration(refName) is not null || cord.GetMachine(refName) is not null;
        if (!resolves) problems.Add($"machine '{m.Name}' constructs from unknown '{refName}'");
    }

    Console.WriteLine($"Model:   {modelType.FullName}");
    Console.WriteLine($"Rules:   {introspector.Rules.Count} ({string.Join(", ", ruleLabels.OrderBy(x => x))})");
    Console.WriteLine($"Accept:  {introspector.AcceptingConditions.Count} accepting condition(s)");
    Console.WriteLine($"Cord:    {cord.Script.Configurations.Count} config(s), {cord.Script.Machines.Count} machine(s)");

    if (problems.Count == 0)
    {
        Console.WriteLine("validate: OK");
        return 0;
    }

    Console.Error.WriteLine($"validate: {problems.Count} problem(s):");
    foreach (var p in problems) Console.Error.WriteLine($"  - {p}");
    return 1;
}

// --- test / run (conformance) --------------------------------------------------

int CmdTest(string[] rest)
{
    if (rest.Length == 0 || IsHelp(rest[0]))
    {
        Console.WriteLine("Usage: sek test <machine> [--project <dir>]");
        return rest.Length == 0 ? 1 : 0;
    }

    var machine = rest[0];
    var solverName = GetOption(rest, "--solver") ?? "z3";
    var (config, dir, cord) = LoadProject(rest);

    if (cord.GetMachine(machine) is null)
    {
        Console.Error.WriteLine($"sek: error: machine '{machine}' not found. Available: {string.Join(", ", cord.Script.Machines.Select(m => m.Name))}");
        return 1;
    }

    if (config.Binding is null)
    {
        Console.Error.WriteLine("sek: error: no 'binding' configured in .specexplorerkit/config.json.");
        return 1;
    }

    var result = ExploreMachine(config, dir, cord, machine, solverName);
    Console.WriteLine($"Explored '{machine}': {result.Graph.States.Count} states, {result.Graph.Transitions.Count} transitions.");

    var bindingAsm = Path.GetFullPath(Path.Combine(dir, config.Binding.Assembly));
    var report = Conformance.Replay(result.Graph, bindingAsm, config.Binding.Namespace);

    Console.WriteLine($"Conformance against SUT ({config.Binding.Namespace}):");
    Console.WriteLine($"  transitions replayed : {report.TransitionsReplayed}");
    Console.WriteLine($"  succeeded            : {report.Succeeded}");
    Console.WriteLine($"  failed               : {report.Failed}");
    Console.WriteLine($"  actions covered      : {report.ActionsCovered.Count} ({string.Join(", ", report.ActionsCovered.OrderBy(x => x))})");

    if (!report.Passed)
    {
        Console.Error.WriteLine("  failures:");
        foreach (var f in report.Failures.Take(20)) Console.Error.WriteLine($"    - {f}");
        Console.WriteLine("TEST FAILED");
        return 1;
    }

    Console.WriteLine("TEST PASSED");
    return 0;
}

// --- generate (test-case generation) -------------------------------------------

int CmdGenerate(string[] rest)
{
    if (rest.Length == 0 || IsHelp(rest[0]))
    {
        Console.WriteLine("Usage: sek generate <machine> [--project <dir>] [--out <dir>] [--namespace <ns>] [--max <n>] [--solver z3|enum]");
        return rest.Length == 0 ? 1 : 0;
    }

    var machine = rest[0];
    var solverName = GetOption(rest, "--solver") ?? "z3";
    var (config, dir, cord) = LoadProject(rest);

    if (cord.GetMachine(machine) is null)
    {
        Console.Error.WriteLine($"sek: error: machine '{machine}' not found. Available: {string.Join(", ", cord.Script.Machines.Select(m => m.Name))}");
        return 1;
    }

    if (config.Binding is null)
    {
        Console.Error.WriteLine("sek: error: test generation needs a 'binding' in .specexplorerkit/config.json (generated tests replay against the SUT).");
        return 1;
    }

    var maxTests = int.TryParse(GetOption(rest, "--max"), out var m) && m > 0 ? m : 50;

    var result = ExploreMachine(config, dir, cord, machine, solverName);
    var graph = result.Graph;
    Console.WriteLine($"Explored '{machine}': {graph.States.Count} states, {graph.Transitions.Count} transitions.");

    var paths = TestGen.SelectPaths(graph, maxTests);
    if (paths.Count == 0)
    {
        Console.Error.WriteLine("sek: error: no test paths could be derived from the exploration.");
        return 1;
    }

    var outDir = GetOption(rest, "--out") ?? Path.Combine(config.ResolveOutDir(dir), machine + "Tests");
    var testNs = GetOption(rest, "--namespace")
                 ?? (string.IsNullOrWhiteSpace(config.Binding.Namespace) ? "SpecExplorerKit.GeneratedTests" : config.Binding.Namespace + ".Tests");
    var bindingAsm = Path.GetFullPath(Path.Combine(dir, config.Binding.Assembly));

    var gen = TestGen.EmitXunit(graph, paths, outDir, testNs, bindingAsm, config.Binding.Namespace);

    Console.WriteLine($"Generated {gen.TestCount} xUnit test(s) covering {gen.CoveredTransitions}/{gen.TotalTransitions} transitions.");
    Console.WriteLine($"Wrote {gen.TestFile}");
    Console.WriteLine($"Run with: dotnet test \"{gen.ProjectDir}\"");
    return 0;
}

int Unknown(string command)
{
    Console.Error.WriteLine($"sek: unknown command '{command}'.");
    PrintUsage();
    return 1;
}

void PrintUsage()
{
    Console.WriteLine($"""
    SpecExplorerKit (sek) {Version} — modern, CLI-first model-based testing (Spec Explorer + Cord revived).

    Usage:
      sek <command> [options]

    Commands:
      init [--project <dir>]        Scaffold a .specexplorerkit/ project
      validate [--project <dir>]    Check the model program and Cord scripts line up
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
    """);
}
