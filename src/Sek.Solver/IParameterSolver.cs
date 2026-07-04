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
