using Sek.Cli;
using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Core.Model;
using Sek.Core.Rendering;
using Sek.Core.Seexpl;
using Sek.Engine;
using Sek.Solver;
using System.Reflection;

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
    if (switches.TryGetValue("StopAtError", out var sae) && string.Equals(sae, "true", StringComparison.OrdinalIgnoreCase)) options.StopAtError = true;
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
    return explorer.Explore(machine, ExpandParamMachines(cord, DesugarLet(m.Body)));
}

ExplorationResult ExploreMachine(ProjectConfig config, string dir, CordDocument cord, string machine, string solverName)
{
    // Scope-aware model loading: a `construct model program … where scope = "Ns.Sub"` selects
    // the model program whose namespace is the scope; otherwise the project's default type.
    var scope = ResolveModelScope(cord, cord.GetMachine(machine)?.Body);
    var modelType = ModelLoader.LoadModelTypeInScope(config.ResolveModelAssembly(dir), scope, config.Model.Type);
    var introspector = new ModelIntrospector(modelType);
    var options = BoundsFor(cord, machine);
    var binds = new Dictionary<string, List<List<string>>>(StringComparer.Ordinal);
    return Interpret(introspector, cord, machine, cord.GetMachine(machine)?.Body, options, solverName, binds);
}

// Finds the model `scope` a machine explores under (the first `construct model program … where
// scope = "…"` reachable through machine references), or null when there is none.
static string? ResolveModelScope(CordDocument cord, Sek.Cord.Ast.Behavior? body)
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

// Recursively interprets a machine body: `bind`, scenario slicing (`||`), and the construct
// family (model program, bounded exploration, accepting paths, test cases, point shoot,
// accept completion, requirement coverage).
ExplorationResult Interpret(
    ModelIntrospector introspector, CordDocument cord, string machine,
    Sek.Cord.Ast.Behavior? body, ExplorationOptions options, string solverName,
    Dictionary<string, List<List<string>>> binds)
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
            return Interpret(introspector, cord, invp.Target, innerMachine.Body, options, solverName, binds);
        }

        return Interpret(introspector, cord, machine, pc.Inner, options, solverName, binds);
    }

    if (body is Sek.Cord.Ast.BindBehavior bind)
    {
        foreach (var c in bind.Binds) binds[c.Action] = c.ArgDomains;
        return Interpret(introspector, cord, machine, bind.Inner, options, solverName, binds);
    }

    var extracted = ExtractSlice(cord, body);
    if (extracted.Config is not null)
    {
        var cfg = extracted.Config;
        var pg = BuildParamGen(cord, cfg, machine, solverName, binds, introspector);
        var explorer = new Explorer(introspector, options, pg);
        var shortNames = introspector.Rules.Select(r => ShortLabel(r.ActionLabel)).ToHashSet(StringComparer.Ordinal);
        var compiled = new BehaviorExplorer(name => cord.GetMachine(name)?.Body, shortNames).Compile(ExpandParamMachines(cord, DesugarLet(extracted.Scenario)));
        return explorer.ExploreSliced(machine, compiled, ShortLabel);
    }

    if (body is Sek.Cord.Ast.ConstructBehavior cb)
    {
        return InterpretConstruct(introspector, cord, machine, cb, options, solverName, binds);
    }

    // A pure behavior (a scenario / parametrized `let`) with no model program is explored
    // directly as a behavior automaton over its declared action alphabet.
    var behaviorGraph = ExploreBehavior(cord, machine);
    return new ExplorationResult { Graph = behaviorGraph };
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
            // Steering: explore the target, mark states satisfying the `with (. expr .)` goal
            // predicate, then prune to paths that actually reach a goal state.
            if (cb.Params.TryGetValue("PathDepth", out var psd) && int.TryParse(psd, out var pdp)) options.MaxDepth = pdp;
            options.GoalPredicate = BuildGoalPredicate(introspector, cb.Params.GetValueOrDefault("with"));
            var r = InterpretTarget(introspector, cord, machine, cb, options, solverName, binds);
            if (options.GoalPredicate is not null)
            {
                var goals = (r.Graph.Metadata.GetValueOrDefault("goals") ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                FilterToReaching(r.Graph, s => goals.Contains(s.Id));
                r.Graph.Metadata["goalCount"] = goals.Count.ToString();
            }

            return r;
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
static void FilterToAcceptingPaths(ExplorationGraph graph) =>
    Sek.Core.Analysis.GraphAnalysis.FilterToReaching(graph, s => s.Accepting);

// Keep only states that lie on a path from the initial state to a "target" state (a state
// matching <paramref name="isTarget"/>). Used by accepting-paths, point-shoot (goal states),
// and accept-completion (accepting states).
static void FilterToReaching(ExplorationGraph graph, Func<Sek.Core.Model.ModelState, bool> isTarget) =>
    Sek.Core.Analysis.GraphAnalysis.FilterToReaching(graph, isTarget);

// Builds a goal-state predicate from a Cord `with (. expr .)` steering condition. The common
// form names a boolean state member (`[Ns.]Type.Member` or a bare `Member`), which is read by
// reflection on the current model instance. Returns null when there is no condition.
static Func<object, bool>? BuildGoalPredicate(ModelIntrospector introspector, string? withExpr)
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

static string ShortLabel(string label)
{
    var i = label.LastIndexOf('.');
    return i >= 0 ? label[(i + 1)..] : label;
}

// Executes a Cord state-slice preconstraint `{. Left = literal; ... .}` by setting the named
// static field/property on a type in the model assembly. The left-hand qualifier (e.g.
// `ModelProgram.` / `Parameters.`) is a namespace hint; the last segment is the member name.
static void ApplyStateSlice(ModelIntrospector introspector, string code)
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

static object? ConvertLiteral(string token, Type type)
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
static (Sek.Cord.Ast.Behavior Scenario, string? Config) ExtractSlice(CordDocument cord, Sek.Cord.Ast.Behavior? body)
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
static string? IsPureModelProgram(CordDocument cord, Sek.Cord.Ast.Behavior item)
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

// Expands parameterized-machine invocations: `machine AnyRequest(int id) { …(id)… }` used as
// `AnyRequest(5)` is replaced by the machine's body with its parameters substituted by the
// call arguments. Runs after DesugarLet so let-bound values are already in the arguments.
static Sek.Cord.Ast.Behavior ExpandParamMachines(CordDocument cord, Sek.Cord.Ast.Behavior b)
{
    switch (b)
    {
        case Sek.Cord.Ast.InvocationBehavior inv when inv.Args is { Count: > 0 } && cord.GetMachine(inv.Target) is { } m && m.Parameters.Count > 0:
        {
            var subst = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < m.Parameters.Count && i < inv.Args.Count; i++) subst[m.Parameters[i].Name] = inv.Args[i];
            return ExpandParamMachines(cord, CloneWithSubst(m.Body, subst));
        }
        case Sek.Cord.Ast.SequenceBehavior s:
        { var n = new Sek.Cord.Ast.SequenceBehavior(); n.Items.AddRange(s.Items.Select(i => ExpandParamMachines(cord, i))); return n; }
        case Sek.Cord.Ast.ChoiceBehavior c:
        { var n = new Sek.Cord.Ast.ChoiceBehavior(); n.Items.AddRange(c.Items.Select(i => ExpandParamMachines(cord, i))); return n; }
        case Sek.Cord.Ast.ParallelBehavior p:
        { var n = new Sek.Cord.Ast.ParallelBehavior { Op = p.Op }; n.Items.AddRange(p.Items.Select(i => ExpandParamMachines(cord, i))); return n; }
        case Sek.Cord.Ast.PermutationBehavior pm:
        { var n = new Sek.Cord.Ast.PermutationBehavior(); n.Items.AddRange(pm.Items.Select(i => ExpandParamMachines(cord, i))); return n; }
        case Sek.Cord.Ast.LooseSequenceBehavior ls:
        { var n = new Sek.Cord.Ast.LooseSequenceBehavior(); n.Items.AddRange(ls.Items.Select(i => ExpandParamMachines(cord, i))); return n; }
        case Sek.Cord.Ast.RepetitionBehavior r:
            return new Sek.Cord.Ast.RepetitionBehavior { Inner = ExpandParamMachines(cord, r.Inner), Op = r.Op, Min = r.Min, Max = r.Max };
        case Sek.Cord.Ast.GroupBehavior g:
            return new Sek.Cord.Ast.GroupBehavior { Inner = ExpandParamMachines(cord, g.Inner) };
        case Sek.Cord.Ast.PreconstraintBehavior pc:
            return new Sek.Cord.Ast.PreconstraintBehavior { Code = pc.Code, Inner = ExpandParamMachines(cord, pc.Inner) };
        case Sek.Cord.Ast.FailBehavior fb:
            return new Sek.Cord.Ast.FailBehavior { Inner = ExpandParamMachines(cord, fb.Inner) };
        default:
            return b;
    }
}

// True if a behavior references any of the given variable names as an invocation argument.
static bool ReferencesVars(Sek.Cord.Ast.Behavior b, HashSet<string> vars)
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
static Sek.Cord.Ast.Behavior DesugarLet(Sek.Cord.Ast.Behavior b)
{
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

            var inner = DesugarLet(let.Inner);
            var choices = new List<Sek.Cord.Ast.Behavior>();
            foreach (var a in assignments)
            {
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
        { var n = new Sek.Cord.Ast.SequenceBehavior(); n.Items.AddRange(s.Items.Select(DesugarLet)); return n; }
        case Sek.Cord.Ast.ChoiceBehavior c:
        { var n = new Sek.Cord.Ast.ChoiceBehavior(); n.Items.AddRange(c.Items.Select(DesugarLet)); return n; }
        case Sek.Cord.Ast.ParallelBehavior p:
        { var n = new Sek.Cord.Ast.ParallelBehavior { Op = p.Op }; n.Items.AddRange(p.Items.Select(DesugarLet)); return n; }
        case Sek.Cord.Ast.PermutationBehavior pm:
        { var n = new Sek.Cord.Ast.PermutationBehavior(); n.Items.AddRange(pm.Items.Select(DesugarLet)); return n; }
        case Sek.Cord.Ast.LooseSequenceBehavior ls:
        { var n = new Sek.Cord.Ast.LooseSequenceBehavior(); n.Items.AddRange(ls.Items.Select(DesugarLet)); return n; }
        case Sek.Cord.Ast.RepetitionBehavior r:
            return new Sek.Cord.Ast.RepetitionBehavior { Inner = DesugarLet(r.Inner), Op = r.Op, Min = r.Min, Max = r.Max };
        case Sek.Cord.Ast.GroupBehavior gg:
            return new Sek.Cord.Ast.GroupBehavior { Inner = DesugarLet(gg.Inner) };
        case Sek.Cord.Ast.PreconstraintBehavior pc:
            return new Sek.Cord.Ast.PreconstraintBehavior { Code = pc.Code, Inner = DesugarLet(pc.Inner) };
        case Sek.Cord.Ast.FailBehavior fb:
            return new Sek.Cord.Ast.FailBehavior { Inner = DesugarLet(fb.Inner) };
        case Sek.Cord.Ast.BindBehavior bd:
        { var n = new Sek.Cord.Ast.BindBehavior { Inner = DesugarLet(bd.Inner) }; n.Binds.AddRange(bd.Binds); return n; }
        default:
            return b;
    }
}

// Deep-clones a behavior, replacing invocation argument tokens that name a bound variable
// with its assigned value.
static Sek.Cord.Ast.Behavior CloneWithSubst(Sek.Cord.Ast.Behavior b, Dictionary<string, string> subst)
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

static ValueKind KindOf(string type)
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

ParameterGeneration BuildParamGen(
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
