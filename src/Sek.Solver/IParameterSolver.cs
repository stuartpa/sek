namespace Sek.Solver;

/// <summary>Generates concrete parameter-value combinations that satisfy constraints.</summary>
public interface IParameterSolver
{
    /// <summary>Backend name (e.g. "z3", "enumerative").</summary>
    string Name { get; }

    IReadOnlyList<IReadOnlyDictionary<string, object?>> Generate(
        IReadOnlyList<SolverParam> parameters,
        IReadOnlyList<SolverConstraint> constraints,
        CombinationSpec combination,
        int limit);
}

/// <summary>Pairwise (2-wise) combinatorial reduction over a set of full combinations.</summary>
public static class Combinatorics
{
    /// <summary>
    /// Applies a <see cref="CombinationSpec"/> to the full set of satisfying combinations:
    /// an optional pairwise reduction, then <c>Expand</c> (force full coverage of listed
    /// params), <c>Isolated</c> (keep one representative per predicate), and <c>Seeded</c>
    /// (guarantee specific combinations are present). Deterministic and order-stable.
    /// </summary>
    public static List<IReadOnlyDictionary<string, object?>> Apply(
        IReadOnlyList<string> paramNames,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> all,
        CombinationSpec spec,
        int limit)
    {
        var work = spec.Mode == CombinationSpec.Strategy.Pairwise
            ? Pairwise(paramNames, all)
            : all.ToList();

        // Pairwise over derived expression columns (e.g. Combination.Pairwise(name, days & 0x1, ..)):
        // compute each column per combination, then cover value-pairs of the columns.
        if (spec.PairwiseColumns.Count > 0)
        {
            var colNames = spec.PairwiseColumns.Select(c => c.Name).ToList();
            var augmented = all.Select(a =>
            {
                var d = new Dictionary<string, object?>(a);
                foreach (var col in spec.PairwiseColumns) d[col.Name] = PredicateEval.Evaluate(col.Expr, a);
                return (IReadOnlyDictionary<string, object?>)d;
            }).ToList();
            work = Pairwise(colNames, augmented);
        }

        // Expand: ensure every distinct value-tuple of the listed params (as seen across all
        // satisfying combinations) is represented in the result.
        var expand = spec.Expand.Where(paramNames.Contains).ToList();
        if (expand.Count > 0)
        {
            string Key(IReadOnlyDictionary<string, object?> c) =>
                string.Join("|", expand.Select(p => c.TryGetValue(p, out var v) ? (v?.ToString() ?? "null") : "null"));
            var have = new HashSet<string>(work.Select(Key));
            foreach (var c in all)
            {
                if (have.Add(Key(c))) work.Add(c);
            }
        }

        // Isolated: keep only the first representative combination satisfying each predicate.
        foreach (var pred in spec.Isolated)
        {
            var kept = false;
            work = work.Where(c =>
            {
                if (!PredicateEval.Eval(pred, c)) return true;
                if (kept) return false;
                kept = true;
                return true;
            }).ToList();
        }

        // Seeded: ensure at least one combination satisfying each seed conjunction is present.
        foreach (var seed in spec.Seeded)
        {
            bool Satisfies(IReadOnlyDictionary<string, object?> c) => seed.All(e => PredicateEval.Eval(e, c));
            if (!work.Any(Satisfies))
            {
                var found = all.FirstOrDefault(Satisfies);
                if (found is not null) work.Add(found);
            }
        }

        return work.Take(limit).ToList();
    }

    /// <summary>
    /// Greedy pairwise selection: keep the smallest subset of <paramref name="allCombos"/>
    /// that still covers every value-pair (across parameter positions) present in the set.
    /// </summary>
    public static List<IReadOnlyDictionary<string, object?>> Pairwise(
        IReadOnlyList<string> paramNames,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> allCombos)
    {
        if (allCombos.Count == 0 || paramNames.Count <= 1)
        {
            return allCombos.ToList();
        }

        // Collect every value-pair (pi=vi, pj=vj) that appears somewhere.
        var required = new HashSet<string>();
        foreach (var combo in allCombos)
        {
            for (var i = 0; i < paramNames.Count; i++)
            {
                for (var j = i + 1; j < paramNames.Count; j++)
                {
                    required.Add(PairKey(paramNames[i], combo[paramNames[i]], paramNames[j], combo[paramNames[j]]));
                }
            }
        }

        var chosen = new List<IReadOnlyDictionary<string, object?>>();
        var covered = new HashSet<string>();

        while (covered.Count < required.Count)
        {
            IReadOnlyDictionary<string, object?>? best = null;
            var bestGain = -1;

            foreach (var combo in allCombos)
            {
                var gain = 0;
                for (var i = 0; i < paramNames.Count; i++)
                {
                    for (var j = i + 1; j < paramNames.Count; j++)
                    {
                        var key = PairKey(paramNames[i], combo[paramNames[i]], paramNames[j], combo[paramNames[j]]);
                        if (!covered.Contains(key))
                        {
                            gain++;
                        }
                    }
                }

                if (gain > bestGain)
                {
                    bestGain = gain;
                    best = combo;
                }
            }

            if (best is null || bestGain <= 0)
            {
                break;
            }

            chosen.Add(best);
            for (var i = 0; i < paramNames.Count; i++)
            {
                for (var j = i + 1; j < paramNames.Count; j++)
                {
                    covered.Add(PairKey(paramNames[i], best[paramNames[i]], paramNames[j], best[paramNames[j]]));
                }
            }
        }

        return chosen;
    }

    private static string PairKey(string a, object? av, string b, object? bv) =>
        $"{a}={av ?? "null"}|{b}={bv ?? "null"}";
}
