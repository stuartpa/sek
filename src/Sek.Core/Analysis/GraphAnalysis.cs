using Sek.Core.Model;

namespace Sek.Core.Analysis;

/// <summary>
/// Pure graph algorithms over an <see cref="ExplorationGraph"/>, shared by the steering
/// constructs (accepting paths, point-shoot goals, accept-completion).
/// </summary>
public static class GraphAnalysis
{
    /// <summary>
    /// Prunes <paramref name="graph"/> in place to only those states that lie on a path from the
    /// initial state to a <em>target</em> state (a state matching <paramref name="isTarget"/>),
    /// via backward reachability from the targets. Transitions are kept only when both endpoints
    /// survive; the initial state is always retained so the graph stays rooted.
    /// </summary>
    /// <returns>The number of target states found.</returns>
    public static int FilterToReaching(ExplorationGraph graph, Func<ModelState, bool> isTarget)
    {
        var targets = graph.States.Where(isTarget).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var canReach = new HashSet<string>(targets, StringComparer.Ordinal);

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var t in graph.Transitions)
            {
                if (canReach.Contains(t.ToStateId) && canReach.Add(t.FromStateId))
                {
                    changed = true;
                }
            }
        }

        graph.Transitions.RemoveAll(t => !canReach.Contains(t.FromStateId) || !canReach.Contains(t.ToStateId));
        graph.States.RemoveAll(s => !canReach.Contains(s.Id) && !s.Initial);
        return targets.Count;
    }
}
