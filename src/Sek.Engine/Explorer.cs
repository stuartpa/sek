using System.Collections;
using System.Reflection;
using System.Text.Json;
using Sek.Core.Model;
using Sek.Modeling;
using Sek.Solver;

namespace Sek.Engine;

/// <summary>Outcome of an exploration: the graph plus summary diagnostics.</summary>
public sealed class ExplorationResult
{
    public required ExplorationGraph Graph { get; init; }
    public bool HitBound { get; init; }
    public List<string> Diagnostics { get; } = new();
}

/// <summary>
/// Deterministic breadth-first explorer over a <see cref="ModelProgram"/> subclass.
///
/// For each reachable state and each rule, it enumerates the cartesian product of the
/// rule parameters' domains, invokes the rule on a fresh copy of the state, and — if
/// the guards pass — records the successor state and a labeled transition. States are
/// de-duplicated by canonical JSON hash.
/// </summary>
public sealed class Explorer
{
    private readonly ModelIntrospector _model;
    private readonly ExplorationOptions _options;
    private readonly ParameterGeneration? _paramGen;
    private readonly Dictionary<string, List<object?[]>> _argSetCache = new();
    private readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public Explorer(ModelIntrospector model, ExplorationOptions? options = null, ParameterGeneration? parameterGeneration = null)
    {
        _model = model;
        _options = options ?? new ExplorationOptions();
        _paramGen = parameterGeneration;
    }

    public ExplorationResult Explore(string machineName)
    {
        var graph = new ExplorationGraph { Machine = machineName };
        var result = new ExplorationResult { Graph = graph };

        var hashToId = new Dictionary<string, string>();
        var queue = new Queue<(string Id, string Json, int Depth)>();
        var hitBound = false;

        // Initial state.
        var initialInstance = CreateInstance();
        var initialJson = Serialize(initialInstance);
        var initialCanonical = CanonicalJson.Canonicalize(initialJson);
        var initialHash = CanonicalJson.Hash(initialCanonical);
        var initialId = "S0";
        hashToId[initialHash] = initialId;
        graph.InitialStateId = initialId;
        graph.States.Add(new ModelState(initialId, initialHash, Label: null, Accepting: IsAccepting(initialInstance), Initial: true));
        queue.Enqueue((initialId, initialJson, 0));

        var nextId = 1;

        while (queue.Count > 0)
        {
            var (fromId, fromJson, depth) = queue.Dequeue();

            if (depth >= _options.MaxDepth)
            {
                continue;
            }

            // A state instance used to evaluate domains (domains may read state).
            var domainInstance = Deserialize(fromJson);

            foreach (var rule in _model.Rules)
            {
                var domainValues = ResolveDomains(rule, domainInstance);
                foreach (var argSet in GenerateArgSets(rule, domainValues))
                {
                    var target = Deserialize(fromJson);
                    var invokeArgs = MaterializeArgs(rule, target, argSet);
                    if (!TryInvoke(rule, target, invokeArgs, result.Diagnostics))
                    {
                        continue; // guard disabled or error -> action not enabled
                    }

                    var toJson = Serialize(target);
                    var toCanonical = CanonicalJson.Canonicalize(toJson);
                    var toHash = CanonicalJson.Hash(toCanonical);

                    if (!hashToId.TryGetValue(toHash, out var toId))
                    {
                        if (graph.States.Count >= _options.MaxStates)
                        {
                            hitBound = true;
                            continue;
                        }

                        toId = "S" + nextId++;
                        hashToId[toHash] = toId;
                        graph.States.Add(new ModelState(toId, toHash, Label: null, Accepting: IsAccepting(target)));
                        queue.Enqueue((toId, toJson, depth + 1));
                    }

                    if (graph.Transitions.Count >= _options.MaxTransitions)
                    {
                        hitBound = true;
                        break;
                    }

                    var action = new ActionInvocation(rule.ActionLabel, invokeArgs.Select(Stringify).ToList());
                    graph.Transitions.Add(new Transition(fromId, action, toId));
                }
            }
        }

        graph.Metadata["states"] = graph.States.Count.ToString();
        graph.Metadata["transitions"] = graph.Transitions.Count.ToString();
        graph.Metadata["accepting"] = graph.States.Count(s => s.Accepting).ToString();
        graph.Metadata["hitBound"] = hitBound.ToString();

        return new ExplorationResult { Graph = graph, HitBound = hitBound };
    }

    /// <summary>
    /// Scenario slicing: explores the model program restricted to the action sequences a
    /// Cord scenario permits (<c>Scenario || ModelProgram</c>). The combined state is
    /// (model state, scenario-automaton state); a rule fires only if the scenario allows
    /// its action from the current scenario state. A combined state is accepting when the
    /// model state is accepting and the scenario is in an accepting state.
    /// </summary>
    public ExplorationResult ExploreSliced(string machineName, BehaviorExplorer.CompiledScenario scenario, Func<string, string> shortOf)
    {
        var graph = new ExplorationGraph { Machine = machineName };
        var result = new ExplorationResult { Graph = graph };
        var hashToId = new Dictionary<string, string>();
        var queue = new Queue<(string Id, string Json, int Dfa, int Depth)>();
        var hitBound = false;

        var initialInstance = CreateInstance();
        var initialJson = Serialize(initialInstance);
        var initialHash = CanonicalJson.Hash(CanonicalJson.Canonicalize(initialJson));
        var initialId = "S0";
        hashToId[initialHash + "@" + scenario.Start] = initialId;
        graph.InitialStateId = initialId;
        graph.States.Add(new ModelState(initialId, initialHash, Label: null,
            Accepting: CombinedAccepting(scenario, initialInstance, scenario.Start), Initial: true));
        queue.Enqueue((initialId, initialJson, scenario.Start, 0));
        var nextId = 1;

        while (queue.Count > 0)
        {
            var (fromId, fromJson, dfaState, depth) = queue.Dequeue();
            if (depth >= _options.MaxDepth) continue;

            var domainInstance = Deserialize(fromJson);
            foreach (var rule in _model.Rules)
            {
                var bareLabel = shortOf(rule.ActionLabel);
                // Quick reject: if the scenario permits neither this action's bare label nor
                // any argument-pinned form of it from here, skip generating arguments at all.
                if (!scenario.Permits(dfaState, bareLabel))
                {
                    continue;
                }

                var domainValues = ResolveDomains(rule, domainInstance);
                // Scenario-supplied arguments: when the scenario pins concrete argument values
                // for this action (e.g. Publish(_, "object1") or a desugared let), feed those
                // values into the value-typed parameter domains so the action can fire with the
                // scenario's values (Spec Explorer's scenario-as-parameter-source semantics).
                var patterns = scenario.ArgPatterns(dfaState, bareLabel).ToList();
                if (patterns.Count > 0)
                {
                    domainValues = ApplyScenarioArgDomains(rule, domainValues, patterns);
                }

                // Event / output value parameters with no Cord domain and no scenario-supplied
                // value draw their domain from the reachable values in the current state (the
                // model determines the observed value, e.g. a delivered message payload).
                for (var pi = 0; pi < rule.Parameters.Count; pi++)
                {
                    var pu = Underlying(rule.Parameters[pi].Type);
                    if (IsModelObjectType(pu) || domainValues[pi].Count > 0) continue;
                    if (HasCordInConstraint(rule, rule.Parameters[pi].Name)) continue;
                    domainValues[pi] = CollectReachableValues(domainInstance, pu);
                }

                foreach (var argSet in GenerateArgSets(rule, domainValues))
                {
                    var target = Deserialize(fromJson);
                    var invokeArgs = MaterializeArgs(rule, target, argSet);

                    // Argument-aware scenario step: match each pinned argument pattern
                    // positionally (a `_` pattern argument is a wildcard), then fall back to
                    // the bare label (any arguments).
                    var normArgs = invokeArgs.Select(a => BehaviorExplorer.NormArg(ArgIdentity(a))).ToList();
                    if (!scenario.TryStepArgs(dfaState, bareLabel, normArgs, out var ndfa) &&
                        !scenario.TryStep(dfaState, bareLabel, out ndfa))
                    {
                        continue; // the scenario does not permit this action with these arguments
                    }

                    if (!TryInvoke(rule, target, invokeArgs, result.Diagnostics))
                    {
                        continue;
                    }

                    var toJson = Serialize(target);
                    var toHash = CanonicalJson.Hash(CanonicalJson.Canonicalize(toJson));
                    var toCombined = toHash + "@" + ndfa;

                    if (!hashToId.TryGetValue(toCombined, out var toId))
                    {
                        if (graph.States.Count >= _options.MaxStates) { hitBound = true; continue; }
                        toId = "S" + nextId++;
                        hashToId[toCombined] = toId;
                        graph.States.Add(new ModelState(toId, toHash, Label: null,
                            Accepting: CombinedAccepting(scenario, target, ndfa)));
                        // Failure states are terminal violations: do not expand past them.
                        if (!scenario.IsFail(ndfa))
                        {
                            queue.Enqueue((toId, toJson, ndfa, depth + 1));
                        }
                    }

                    if (graph.Transitions.Count >= _options.MaxTransitions) { hitBound = true; break; }
                    var action = new ActionInvocation(rule.ActionLabel, invokeArgs.Select(Stringify).ToList());
                    graph.Transitions.Add(new Transition(fromId, action, toId));
                }
            }
        }

        graph.Metadata["states"] = graph.States.Count.ToString();
        graph.Metadata["transitions"] = graph.Transitions.Count.ToString();
        graph.Metadata["accepting"] = graph.States.Count(s => s.Accepting).ToString();
        graph.Metadata["hitBound"] = hitBound.ToString();
        graph.Metadata["mode"] = "sliced";
        if (scenario.HasFailStates)
        {
            graph.Metadata["modelCheck"] = "true";
        }

        return new ExplorationResult { Graph = graph, HitBound = hitBound };
    }

    /// <summary>Whether a combined (model, scenario) state is a goal state. For a model-checking
    /// scenario (one with <c>: fail</c> states), the goal is a reached failure state; otherwise
    /// it is a model-accepting state at a scenario-accepting state.</summary>
    private bool CombinedAccepting(BehaviorExplorer.CompiledScenario scenario, object model, int dfa) =>
        scenario.HasFailStates
            ? scenario.IsFail(dfa)
            : IsAccepting(model) && scenario.IsAccepting(dfa);

    private ModelProgram CreateInstance() =>
        (ModelProgram)(Activator.CreateInstance(_model.ModelType)
                       ?? throw new InvalidOperationException($"Cannot instantiate '{_model.ModelType.Name}'."));

    private string Serialize(object instance) => JsonSerializer.Serialize(instance, _model.ModelType, _json);

    private ModelProgram Deserialize(string json) =>
        (ModelProgram)(JsonSerializer.Deserialize(json, _model.ModelType, _json)
                       ?? throw new InvalidOperationException("Failed to deserialize model state."));

    private bool IsAccepting(object instance)
    {
        foreach (var cond in _model.AcceptingConditions)
        {
            var value = cond.IsStatic ? cond.Invoke(null, null) : cond.Invoke(instance, null);
            if (value is bool b && !b)
            {
                return false;
            }
        }

        // No accepting conditions => no state is accepting.
        return _model.AcceptingConditions.Count > 0;
    }

    private List<List<object?>> ResolveDomains(RuleInfo rule, object domainInstance)
    {
        var result = new List<List<object?>>();
        foreach (var p in rule.Parameters)
        {
            var values = new List<object?>();

            if (!string.IsNullOrEmpty(p.DomainMethod))
            {
                var method = _model.GetDomainMethod(p.DomainMethod);
                var raw = method.IsStatic ? method.Invoke(null, null) : method.Invoke(domainInstance, null);
                if (raw is IEnumerable en && raw is not string)
                {
                    foreach (var v in en)
                    {
                        values.Add(Coerce(v, p.Type));
                    }
                }
            }
            else
            {
                // No [Domain]: derive a natural domain for enums/bools; leave value-typed
                // params (string/int/...) empty so Cord `Condition.In` supplies the domain.
                var u = Underlying(p.Type);
                if (u.IsEnum)
                {
                    foreach (var v in Enum.GetValues(u))
                    {
                        values.Add(v);
                    }
                }
                else if (u == typeof(bool))
                {
                    values.Add(false);
                    values.Add(true);
                }
                else if (IsModelObjectType(u))
                {
                    // Reference-typed parameter: its domain is the objects of that type
                    // reachable in the current state (Spec Explorer's "live object" domain),
                    // represented by index into the deterministic reachable list. The actual
                    // object is materialized against the mutated instance at invoke time, so
                    // mutations via the parameter land on the state being explored. Fully
                    // general - the engine has no knowledge of any specific model.
                    var count = CollectReachable(domainInstance, u).Count;
                    for (var k = 0; k < count; k++)
                    {
                        values.Add(k);
                    }
                }
            }

            result.Add(values);
        }

        return result;
    }

    private static object? Coerce(object? value, Type target)
    {
        if (value is null)
        {
            return null;
        }

        if (target.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            return Convert.ChangeType(value, target);
        }
        catch
        {
            return value;
        }
    }

    private static bool TryInvoke(RuleInfo rule, ModelProgram target, object?[] args, List<string> diagnostics)
    {
        try
        {
            if (rule.Method.IsStatic)
            {
                rule.Method.Invoke(null, args);
            }
            else
            {
                rule.Method.Invoke(target, args);
            }

            return true;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is GuardDisabledException)
        {
            return false; // guard not satisfied: action disabled
        }
        catch (TargetInvocationException tie)
        {
            diagnostics.Add($"{rule.ActionLabel}: {tie.InnerException?.GetType().Name}: {tie.InnerException?.Message}");
            return false;
        }
    }

    /// <summary>
    /// Applies scenario-supplied argument values to the parameter domains: for each value-typed
    /// parameter position that the scenario pins to a concrete value (not <c>_</c>), the pinned
    /// value is unioned into that parameter's candidate domain. This lets a sliced action fire
    /// with the values the scenario names (e.g. <c>Publish(_, "object1")</c>).
    /// </summary>
    private List<List<object?>> ApplyScenarioArgDomains(RuleInfo rule, List<List<object?>> domainValues, List<string[]> patterns)
    {
        var result = domainValues.Select(d => new List<object?>(d)).ToList();
        for (var i = 0; i < rule.Parameters.Count; i++)
        {
            var u = Underlying(rule.Parameters[i].Type);
            if (IsModelObjectType(u)) continue; // object params use the reachable-object domain

            foreach (var pat in patterns)
            {
                if (i >= pat.Length) continue;
                var tok = pat[i];
                if (tok == "_") continue;
                var val = ParseScenarioValue(tok, rule.Parameters[i].Type);
                if (val is not null && !result[i].Any(v => Equals(v, val))) result[i].Add(val);
            }
        }

        return result;
    }

    private static object? ParseScenarioValue(string token, Type type)
    {
        var t = Underlying(type);
        if (t.IsEnum)
        {
            try { return Enum.Parse(t, token, true); } catch { return null; }
        }

        if (t == typeof(string)) return token;
        if (t == typeof(bool)) return bool.TryParse(token, out var b) ? b : (object?)null;
        if (t == typeof(long) || t == typeof(ulong)) return long.TryParse(token, out var l) ? l : (object?)null;
        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            return double.TryParse(token, out var d) ? Convert.ChangeType(d, t) : (object?)null;
        return int.TryParse(token, out var n) ? n : (object?)null;
    }

    /// <summary>
    /// Replaces object-parameter indices in <paramref name="argSet"/> with the actual objects
    /// reachable from <paramref name="target"/> (the instance the rule will mutate), so that
    /// mutations via a reference parameter land on the state being explored. Value parameters
    /// pass through unchanged.
    /// </summary>
    private object?[] MaterializeArgs(RuleInfo rule, ModelProgram target, object?[] argSet)
    {
        var needsObjects = rule.Parameters.Any(p => IsModelObjectType(Underlying(p.Type)));
        if (!needsObjects)
        {
            return argSet;
        }

        var reachableByType = new Dictionary<Type, List<object?>>();
        var args = new object?[rule.Parameters.Count];
        for (var i = 0; i < rule.Parameters.Count; i++)
        {
            var u = Underlying(rule.Parameters[i].Type);
            if (IsModelObjectType(u) && argSet[i] is int idx)
            {
                if (!reachableByType.TryGetValue(u, out var objs))
                {
                    objs = CollectReachable(target, u);
                    reachableByType[u] = objs;
                }

                args[i] = idx >= 0 && idx < objs.Count ? objs[idx] : null;
            }
            else
            {
                args[i] = argSet[i];
            }
        }

        return args;
    }

    /// <summary>True if <paramref name="p"/> is a struct-valued parameter: a class type that
    /// has Cord field domains (e.g. Condition.In(info.Command, ..)).</summary>
    private static bool IsStructValueParam(RuleParameter p, IReadOnlyList<SolverConstraint> constraints)
    {
        if (p.Type.IsByRef) return false;
        var u = Underlying(p.Type);
        if (!IsModelObjectType(u)) return false;
        var prefix = p.Name + ".";
        return constraints.OfType<InConstraint>().Any(c => c.Param.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static IEnumerable<(string Name, Type Type)> StructMembers(Type t)    {
        foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanWrite && prop.GetIndexParameters().Length == 0) yield return (prop.Name, prop.PropertyType);
        }

        foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return (field.Name, field.FieldType);
        }
    }

    private static void SetMember(object instance, string name, object? value)
    {
        var t = instance.GetType();
        var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop is not null && prop.CanWrite) { prop.SetValue(instance, value); return; }
        var field = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        field?.SetValue(instance, value);
    }

    /// <summary>The natural candidate domain for an enum (all members) or bool ({false,true});
    /// null for other types (which require an explicit Cord domain).</summary>
    private static IReadOnlyList<object?>? NaturalDomain(Type type)
    {
        var u = Underlying(type);
        if (u.IsEnum) return Enum.GetValues(u).Cast<object?>().Distinct().ToList();
        if (u == typeof(bool)) return new object?[] { false, true };
        return null;
    }

    /// <summary>
    /// Generates argument sets for a rule with struct-valued parameters: each struct field
    /// that has a Cord domain becomes a virtual scalar parameter (named <c>param.field</c>);
    /// the solver combines them (honoring Condition.IsTrue / Combination over field names),
    /// and each assignment is materialized into constructed struct instances. Out/ref
    /// parameters are left as default output slots.
    /// </summary>
    private IEnumerable<object?[]> GenerateStructArgSets(RuleInfo rule, IReadOnlyList<SolverConstraint> constraints, CombinationSpec combination)
    {
        var inByName = constraints.OfType<InConstraint>()
            .GroupBy(c => c.Param).ToDictionary(g => g.Key, g => g.Last().Values, StringComparer.Ordinal);

        var virtualParams = new List<SolverParam>();
        var slots = new List<(int ParamIndex, string? Field, Type MemberType)>();

        for (var i = 0; i < rule.Parameters.Count; i++)
        {
            var p = rule.Parameters[i];
            if (p.Type.IsByRef) continue; // out/ref: an output slot, not generated
            var u = Underlying(p.Type);

            if (IsStructValueParam(p, constraints))
            {
                foreach (var (fieldName, memberType) in StructMembers(u))
                {
                    var vname = p.Name + "." + fieldName;
                    // A field's domain is its Cord Condition.In values, or the natural domain
                    // for an enum/bool field; scalar fields with neither are left at default.
                    IReadOnlyList<object?>? dom = inByName.TryGetValue(vname, out var vals)
                        ? vals
                        : NaturalDomain(memberType);
                    if (dom is null) continue;
                    virtualParams.Add(new SolverParam { Name = vname, Kind = Kind(memberType), Domain = dom });
                    slots.Add((i, fieldName, memberType));
                }
            }
            else
            {
                var dom = inByName.TryGetValue(p.Name, out var vals) ? vals : null;
                virtualParams.Add(new SolverParam { Name = p.Name, Kind = Kind(p.Type), Domain = dom });
                slots.Add((i, null, u));
            }
        }

        foreach (var assignment in _paramGen!.Solver.Generate(virtualParams, constraints, combination, _paramGen.Limit))
        {
            var args = new object?[rule.Parameters.Count];
            var structInstances = new Dictionary<int, object>();
            for (var v = 0; v < virtualParams.Count; v++)
            {
                var (pi, field, mtype) = slots[v];
                assignment.TryGetValue(virtualParams[v].Name, out var val);
                var coerced = Coerce(val, mtype);
                if (field is null)
                {
                    args[pi] = coerced;
                }
                else
                {
                    if (!structInstances.TryGetValue(pi, out var inst))
                    {
                        inst = Activator.CreateInstance(Underlying(rule.Parameters[pi].Type))!;
                        structInstances[pi] = inst;
                    }

                    SetMember(inst, field, coerced);
                }
            }

            foreach (var kv in structInstances) args[kv.Key] = kv.Value;
            yield return args;
        }
    }

    private IEnumerable<object?[]> GenerateArgSets(RuleInfo rule, List<List<object?>> domainValues)
    {
        if (_paramGen is null || rule.Parameters.Count == 0)
        {
            foreach (var c in CartesianProduct(domainValues)) yield return c;
            yield break;
        }
        // Per-action constraints from Cord (Condition.In / Condition.IsTrue / Combination).
        IReadOnlyList<SolverConstraint> constraints = Array.Empty<SolverConstraint>();
        var combination = new CombinationSpec();
        if (_paramGen.ByAction.TryGetValue(rule.ActionLabel, out var spec))
        {
            constraints = ResolveEnumLiterals(rule, spec.Constraints);
            combination = ResolveEnumLiterals(rule, spec.Combination);
        }

        // Struct-valued parameters (a class with Cord field domains like Condition.In(info.X, ..))
        // are generated by combining their field domains and constructing instances.
        if (rule.Parameters.Any(p => IsStructValueParam(p, constraints)))
        {
            foreach (var c in GenerateStructArgSets(rule, constraints, combination)) yield return c;
            yield break;
        }

        // Object- and floating-point-typed parameters are not part of the SMT theory the
        // solver models, so they are enumerated directly (cartesian product + predicate
        // filter + optional pairwise). Object domains are state-dependent (reachable objects),
        // so this path is not cached.
        var needsEnumerative = rule.Parameters.Any(p =>
        {
            var u = Underlying(p.Type);
            return u == typeof(float) || u == typeof(double) || u == typeof(decimal) || IsModelObjectType(u);
        });

        if (needsEnumerative)
        {
            var inByParam = constraints.OfType<InConstraint>().ToDictionary(c => c.Param, c => c.Values);
            var effective = new List<List<object?>>(rule.Parameters.Count);
            for (var i = 0; i < rule.Parameters.Count; i++)
            {
                var d = domainValues[i];
                if (d.Count == 0 && inByParam.TryGetValue(rule.Parameters[i].Name, out var inVals))
                {
                    d = inVals.Select(v => Coerce(v, rule.Parameters[i].Type)).ToList();
                }

                effective.Add(d);
            }

            foreach (var c in EnumerateArgSets(rule, effective, constraints, combination)) yield return c;
            yield break;
        }

        // Value-typed parameters (int/bool/string/enum) have state-independent domains, so
        // the solver result is cached per (action, domain values) to avoid re-solving in
        // every state (otherwise catastrophically slow on large graphs). The solver reads the
        // candidate domain from either the passed domain or the Cord Condition.In values.
        var cacheKey = rule.ActionLabel + "::" +
                       string.Join("|", domainValues.Select(d => string.Join(",", d.Select(v => v?.ToString() ?? "null"))));
        if (_argSetCache.TryGetValue(cacheKey, out var cached))
        {
            foreach (var c in cached) yield return c;
            yield break;
        }

        var solverParams = new List<SolverParam>();
        for (var i = 0; i < rule.Parameters.Count; i++)
        {
            solverParams.Add(new SolverParam
            {
                Name = rule.Parameters[i].Name,
                Kind = Kind(rule.Parameters[i].Type),
                Domain = domainValues[i],
            });
        }

        var argSets = new List<object?[]>();
        foreach (var a in _paramGen.Solver.Generate(solverParams, constraints, combination, _paramGen.Limit))
        {
            var args = new object?[rule.Parameters.Count];
            for (var i = 0; i < rule.Parameters.Count; i++)
            {
                a.TryGetValue(rule.Parameters[i].Name, out var v);
                args[i] = Coerce(v, rule.Parameters[i].Type);
            }

            argSets.Add(args);
        }

        _argSetCache[cacheKey] = argSets;
        foreach (var c in argSets) yield return c;
    }

    /// <summary>
    /// Direct enumeration of argument combinations: cartesian product of the effective
    /// domains, filtered by any <see cref="PredicateConstraint"/>, then reduced pairwise if
    /// requested. Used when a rule has object- or floating-point-typed parameters. Object
    /// parameters carry an index into the reachable-object list (materialized at invoke time).
    /// </summary>
    private List<object?[]> EnumerateArgSets(
        RuleInfo rule, List<List<object?>> effective,
        IReadOnlyList<SolverConstraint> constraints, CombinationSpec combination)
    {
        var names = rule.Parameters.Select(p => p.Name).ToList();
        var predicates = constraints.OfType<PredicateConstraint>().Select(p => p.Expr).ToList();

        var combos = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var args in CartesianProduct(effective))
        {
            var dict = new Dictionary<string, object?>(names.Count);
            for (var i = 0; i < names.Count; i++)
            {
                dict[names[i]] = args[i];
            }

            if (predicates.All(e => PredicateEval.Eval(e, dict)))
            {
                combos.Add(dict);
            }
        }

        if (combination.Mode == CombinationSpec.Strategy.Pairwise)
        {
            combos = Combinatorics.Pairwise(names, combos);
        }

        var result = new List<object?[]>(combos.Count);
        foreach (var c in combos)
        {
            var args = new object?[rule.Parameters.Count];
            for (var i = 0; i < rule.Parameters.Count; i++)
            {
                c.TryGetValue(rule.Parameters[i].Name, out var v);
                // Object-typed params carry an index (materialized later); leave it as-is.
                args[i] = IsModelObjectType(Underlying(rule.Parameters[i].Type))
                    ? v
                    : Coerce(v, rule.Parameters[i].Type);
            }

            result.Add(args);
        }

        return result.Take(_paramGen!.Limit).ToList();
    }

    private static bool IsModelObjectType(Type t) =>
        t.IsClass && t != typeof(string) && !t.IsArray;

    /// <summary>
    /// Breadth-first collection of all objects assignable to <paramref name="targetType"/>
    /// reachable from <paramref name="root"/> via public properties/fields and enumerables.
    /// Distinct by reference. Fully model-agnostic.
    /// </summary>
    private static List<object?> CollectReachable(object root, Type targetType)
    {
        var found = new List<object?>();
        var seenFound = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<object>();
        queue.Enqueue(root);
        visited.Add(root);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            var t = cur.GetType();

            if (cur != root && targetType.IsInstanceOfType(cur) && seenFound.Add(cur))
            {
                found.Add(cur);
            }

            if (cur is System.Collections.IEnumerable en && cur is not string)
            {
                foreach (var item in en)
                {
                    if (item is not null && !IsScalar(item.GetType()) && visited.Add(item))
                    {
                        queue.Enqueue(item);
                    }
                }

                continue;
            }

            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length != 0) continue;
                object? val;
                try { val = prop.GetValue(cur); } catch { continue; }
                if (val is not null && !IsScalar(val.GetType()) && visited.Add(val))
                {
                    queue.Enqueue(val);
                }
            }

            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var val = field.GetValue(cur);
                if (val is not null && !IsScalar(val.GetType()) && visited.Add(val))
                {
                    queue.Enqueue(val);
                }
            }
        }

        return found;
    }

    private static bool IsScalar(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsPrimitive || u.IsEnum || u == typeof(string) || u == typeof(decimal)
               || u == typeof(DateTime) || u == typeof(Guid);
    }

    /// <summary>
    /// Breadth-first collection of the distinct scalar values assignable to
    /// <paramref name="targetType"/> reachable from <paramref name="root"/>. Gives event /
    /// output value parameters (e.g. a delivered message payload) their natural domain from
    /// the current model state when no Cord or scenario value is supplied.
    /// </summary>
    private static List<object?> CollectReachableValues(object root, Type targetType)
    {
        var found = new List<object?>();
        var seen = new HashSet<object?>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<object>();
        queue.Enqueue(root);
        visited.Add(root);

        void Consider(object? v)
        {
            if (v is not null && targetType.IsInstanceOfType(v) && seen.Add(v)) found.Add(v);
        }

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            var t = cur.GetType();

            if (cur is System.Collections.IEnumerable en && cur is not string)
            {
                foreach (var item in en)
                {
                    if (item is null) continue;
                    if (IsScalar(item.GetType())) Consider(item);
                    else if (visited.Add(item)) queue.Enqueue(item);
                }

                continue;
            }

            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length != 0) continue;
                object? val;
                try { val = prop.GetValue(cur); } catch { continue; }
                if (val is null) continue;
                if (IsScalar(val.GetType())) Consider(val);
                else if (visited.Add(val)) queue.Enqueue(val);
            }

            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var val = field.GetValue(cur);
                if (val is null) continue;
                if (IsScalar(val.GetType())) Consider(val);
                else if (visited.Add(val)) queue.Enqueue(val);
            }
        }

        return found;
    }

    private bool HasCordInConstraint(RuleInfo rule, string paramName) =>
        _paramGen is not null
        && _paramGen.ByAction.TryGetValue(rule.ActionLabel, out var spec)
        && spec.Constraints.OfType<InConstraint>().Any(c => c.Param == paramName);

    private static Type Underlying(Type t) => Nullable.GetUnderlyingType(t) ?? t;

    /// <summary>
    /// Rewrites enum-qualified identifiers (e.g. <c>Frequency.Daily</c>) inside
    /// <see cref="PredicateConstraint"/> expressions into integer literals, using the
    /// enum types of the rule's parameters. This lets Cord <c>Condition.IsTrue</c>
    /// predicates that compare against enum members be handled by the solver.
    /// </summary>
    private static IReadOnlyList<SolverConstraint> ResolveEnumLiterals(RuleInfo rule, IReadOnlyList<SolverConstraint> constraints)
    {
        var enumTypes = EnumTypes(rule);
        if (enumTypes.Count == 0)
        {
            return constraints;
        }

        var rewritten = new List<SolverConstraint>(constraints.Count);
        foreach (var c in constraints)
        {
            rewritten.Add(c is PredicateConstraint pc
                ? new PredicateConstraint { Expr = RewriteEnum(pc.Expr, enumTypes) }
                : c);
        }

        return rewritten;
    }

    /// <summary>Resolves enum-qualified identifiers inside a combination spec's Isolated and
    /// Seeded predicates (Expand/Mode are unaffected).</summary>
    private static CombinationSpec ResolveEnumLiterals(RuleInfo rule, CombinationSpec c)
    {
        var enumTypes = EnumTypes(rule);
        var needsRewrite = enumTypes.Count > 0 && (c.Isolated.Count > 0 || c.Seeded.Count > 0 || c.PairwiseColumns.Count > 0);
        if (!needsRewrite && c.PairwiseColumns.Count == 0)
        {
            return c;
        }

        var res = new CombinationSpec { Mode = c.Mode };
        res.Expand.AddRange(c.Expand);
        foreach (var e in c.Isolated) res.Isolated.Add(RewriteEnum(e, enumTypes));
        foreach (var seed in c.Seeded) res.Seeded.Add(seed.Select(e => RewriteEnum(e, enumTypes)).ToList());
        foreach (var col in c.PairwiseColumns) res.PairwiseColumns.Add((col.Name, RewriteEnum(col.Expr, enumTypes)));
        return res;
    }

    private static Dictionary<string, Type> EnumTypes(RuleInfo rule)
    {
        var enumTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var p in rule.Parameters)
        {
            var u = Underlying(p.Type);
            if (u.IsEnum)
            {
                enumTypes[u.Name] = u;
            }
        }

        return enumTypes;
    }

    private static Expr RewriteEnum(Expr e, Dictionary<string, Type> enumTypes)
    {
        switch (e)
        {
            case VarExpr v when v.Name.Contains('.'):
            {
                var dot = v.Name.IndexOf('.');
                var typeName = v.Name[..dot];
                var member = v.Name[(dot + 1)..];
                if (enumTypes.TryGetValue(typeName, out var t) && Enum.TryParse(t, member, out var val) && val is not null)
                {
                    return new LitExpr { Value = (int)Convert.ToInt64(val), Kind = ValueKind.Int };
                }

                return v;
            }

            case BinExpr b:
                return new BinExpr { Op = b.Op, Left = RewriteEnum(b.Left, enumTypes), Right = RewriteEnum(b.Right, enumTypes) };
            case UnExpr u:
                return new UnExpr { Op = u.Op, Operand = RewriteEnum(u.Operand, enumTypes) };
            default:
                return e;
        }
    }

    private static ValueKind Kind(Type type)
    {
        var t = Underlying(type);
        if (t.IsEnum) return ValueKind.Int;
        if (t == typeof(bool)) return ValueKind.Bool;
        if (t == typeof(string)) return ValueKind.String;
        if (t == typeof(long) || t == typeof(ulong)) return ValueKind.Long;
        return ValueKind.Int;
    }

    private static IEnumerable<object?[]> CartesianProduct(List<List<object?>> domains)
    {
        if (domains.Count == 0)
        {
            yield return Array.Empty<object?>();
            yield break;
        }

        var indices = new int[domains.Count];
        while (true)
        {
            if (domains.Any(d => d.Count == 0))
            {
                yield break;
            }

            var combo = new object?[domains.Count];
            for (var i = 0; i < domains.Count; i++)
            {
                combo[i] = domains[i][indices[i]];
            }

            yield return combo;

            var pos = domains.Count - 1;
            while (pos >= 0)
            {
                indices[pos]++;
                if (indices[pos] < domains[pos].Count)
                {
                    break;
                }

                indices[pos] = 0;
                pos--;
            }

            if (pos < 0)
            {
                yield break;
            }
        }
    }

    private string Stringify(object? value)
    {
        return value switch
        {
            null => "null",
            string s => s,
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ when IsModelObjectType(value.GetType()) => JsonSerializer.Serialize(value, value.GetType(), _json),
            _ => value.ToString() ?? string.Empty,
        };
    }

    /// <summary>The identity of an argument for scenario matching: for a model object with an
    /// <c>Id</c>/<c>Name</c>/<c>Handle</c> property, that property's value (so a scenario can
    /// pin an object by id, e.g. BroadcastRequest(1, ..)); otherwise the stringified value.</summary>
    private string ArgIdentity(object? value)
    {
        if (value is not null && IsModelObjectType(value.GetType()))
        {
            var t = value.GetType();
            foreach (var name in new[] { "Id", "Name", "Handle" })
            {
                var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop is not null)
                {
                    var v = prop.GetValue(value);
                    if (v is not null) return v.ToString() ?? string.Empty;
                }
            }
        }

        return Stringify(value);
    }
}
