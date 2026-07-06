namespace SpecExplorerKit.Components.Solving;

/// <summary>
/// Dependency-free fallback solver: forms the cartesian product of each parameter's
/// candidate values and filters by the predicate constraints (evaluated in C#).
/// Used when Z3 is unavailable or disabled.
/// </summary>
public sealed class EnumerativeSolver : IParameterSolver
{
    public string Name => "enumerative";

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Generate(
        IReadOnlyList<SolverParam> parameters,
        IReadOnlyList<SolverConstraint> constraints,
        CombinationSpec combination,
        int limit)
    {
        var predicates = constraints.OfType<PredicateConstraint>().ToList();
        var compiled = constraints.OfType<CompiledPredicateConstraint>().ToList();
        var inByParam = constraints.OfType<InConstraint>().ToDictionary(c => c.Param, c => c.Values);

        var domains = new List<List<object?>>();
        foreach (var p in parameters)
        {
            var values = inByParam.TryGetValue(p.Name, out var inVals)
                ? inVals
                : (p.Domain?.ToList() ?? new List<object?>());
            domains.Add(values.ToList());
        }

        var all = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var combo in Cartesian(parameters, domains))
        {
            if (predicates.All(pred => PredicateEval.Eval(pred.Expr, combo))
                && compiled.All(c => c.Predicate(combo)))
            {
                all.Add(combo);
                if (all.Count >= 200000)
                {
                    break;
                }
            }
        }

        return Combinatorics.Apply(parameters.Select(p => p.Name).ToList(), all, combination, limit);
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> Cartesian(
        IReadOnlyList<SolverParam> parameters,
        List<List<object?>> domains)
    {
        if (parameters.Count == 0)
        {
            yield return new Dictionary<string, object?>();
            yield break;
        }

        if (domains.Any(d => d.Count == 0))
        {
            yield break;
        }

        var idx = new int[parameters.Count];
        while (true)
        {
            var combo = new Dictionary<string, object?>();
            for (var i = 0; i < parameters.Count; i++)
            {
                combo[parameters[i].Name] = domains[i][idx[i]];
            }

            yield return combo;

            var pos = parameters.Count - 1;
            while (pos >= 0)
            {
                idx[pos]++;
                if (idx[pos] < domains[pos].Count)
                {
                    break;
                }

                idx[pos] = 0;
                pos--;
            }

            if (pos < 0)
            {
                yield break;
            }
        }
    }
}
