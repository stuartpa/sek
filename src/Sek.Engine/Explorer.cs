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
            Accepting: IsAccepting(initialInstance) && scenario.IsAccepting(scenario.Start), Initial: true));
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
                foreach (var argSet in GenerateArgSets(rule, domainValues))
                {
                    var target = Deserialize(fromJson);
                    var invokeArgs = MaterializeArgs(rule, target, argSet);

                    // Argument-aware scenario step: try the concrete symbol (label with the
                    // transition's argument values) first, then the bare label (any args).
                    var concrete = bareLabel + "(" + string.Join(",", invokeArgs.Select(a => BehaviorExplorer.NormArg(Stringify(a)))) + ")";
                    if (!scenario.TryStep(dfaState, concrete, out var ndfa) &&
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
                            Accepting: IsAccepting(target) && scenario.IsAccepting(ndfa)));
                        queue.Enqueue((toId, toJson, ndfa, depth + 1));
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

        return new ExplorationResult { Graph = graph, HitBound = hitBound };
    }

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
            if (IsModelObjectType(u) && argSet[i] is not null)
            {
                if (!reachableByType.TryGetValue(u, out var objs))
                {
                    objs = CollectReachable(target, u);
                    reachableByType[u] = objs;
                }

                var idx = Convert.ToInt32(argSet[i]);
                args[i] = idx >= 0 && idx < objs.Count ? objs[idx] : null;
            }
            else
            {
                args[i] = argSet[i];
            }
        }

        return args;
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
        if (enumTypes.Count == 0 || (c.Isolated.Count == 0 && c.Seeded.Count == 0))
        {
            return c;
        }

        var res = new CombinationSpec { Mode = c.Mode };
        res.Expand.AddRange(c.Expand);
        foreach (var e in c.Isolated) res.Isolated.Add(RewriteEnum(e, enumTypes));
        foreach (var seed in c.Seeded) res.Seeded.Add(seed.Select(e => RewriteEnum(e, enumTypes)).ToList());
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
}
