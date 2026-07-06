using Sek.Core.Model;
using SpecExplorerKit.Components.Graphs;

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
        var canReach = Reachability.Backward(
            graph.Transitions.Select(t => (t.FromStateId, t.ToStateId)),
            targets,
            StringComparer.Ordinal);

        graph.Transitions.RemoveAll(t => !canReach.Contains(t.FromStateId) || !canReach.Contains(t.ToStateId));
        graph.States.RemoveAll(s => !canReach.Contains(s.Id) && !s.Initial);
        return targets.Count;
    }

    /// <summary>
    /// Merges several exploration graphs into one, unifying states across graphs by their canonical
    /// model-state <see cref="ModelState.Hash"/> (a phase that resumes from a state shares that
    /// state's hash). Accepting is the OR across graphs; the initial state is the one whose hash is
    /// <paramref name="rootHash"/>. Used to stitch the point-shoot Point/Shoot/Completer phases.
    /// </summary>
    public static ExplorationGraph MergeByHash(string machine, IEnumerable<ExplorationGraph> graphs, string? rootHash)
    {
        var merged = new ExplorationGraph { Machine = machine };
        var hashToId = new Dictionary<string, string>(StringComparer.Ordinal);
        var accepting = new Dictionary<string, bool>(StringComparer.Ordinal);
        var next = 0;

        var all = graphs.ToList();
        foreach (var g in all)
        {
            foreach (var s in g.States)
            {
                if (!hashToId.ContainsKey(s.Hash)) hashToId[s.Hash] = "S" + next++;
                accepting[s.Hash] = accepting.GetValueOrDefault(s.Hash) || s.Accepting;
            }
        }

        merged.InitialStateId = rootHash is not null && hashToId.TryGetValue(rootHash, out var rid) ? rid : "S0";

        var edges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in all)
        {
            var byId = g.States.ToDictionary(s => s.Id, s => s.Hash, StringComparer.Ordinal);
            foreach (var t in g.Transitions)
            {
                if (!byId.TryGetValue(t.FromStateId, out var fh) || !byId.TryGetValue(t.ToStateId, out var th)) continue;
                var fromId = hashToId[fh];
                var toId = hashToId[th];
                var key = fromId + "|" + t.Action.Display + "|" + t.Action.Kind + "|" + toId;
                if (edges.Add(key)) merged.Transitions.Add(new Transition(fromId, t.Action, toId));
            }
        }

        foreach (var (hash, id) in hashToId)
        {
            merged.States.Add(new ModelState(id, hash, Label: null, Accepting: accepting.GetValueOrDefault(hash), Initial: id == merged.InitialStateId));
        }

        merged.Metadata["states"] = merged.States.Count.ToString();
        merged.Metadata["transitions"] = merged.Transitions.Count.ToString();
        merged.Metadata["accepting"] = merged.States.Count(s => s.Accepting).ToString();
        return merged;
    }

    /// <summary>
    /// Prunes a stitched point-shoot graph to states on a path root → goal → accepting: everything
    /// that can reach a goal state (identified by <paramref name="goalHashes"/>) plus everything
    /// reachable from a goal that can still reach an accepting state (the completion). No-op when
    /// there are no goal states in the graph.
    /// </summary>
    public static void FilterToGoalThenAccepting(ExplorationGraph graph, ISet<string> goalHashes)
    {
        var goalIds = graph.States.Where(s => goalHashes.Contains(s.Hash)).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        if (goalIds.Count == 0) return;

        var edges = graph.Transitions.Select(t => (t.FromStateId, t.ToStateId)).ToList();

        var canReachGoal = Reachability.Backward(edges, goalIds, StringComparer.Ordinal);
        var canReachAccept = Reachability.Backward(edges, graph.States.Where(s => s.Accepting).Select(s => s.Id), StringComparer.Ordinal);
        var fromGoal = Reachability.Forward(edges, goalIds, StringComparer.Ordinal);

        var keep = new HashSet<string>(canReachGoal, StringComparer.Ordinal);
        keep.UnionWith(fromGoal.Where(canReachAccept.Contains)); // goal → accepting completion
        keep.UnionWith(goalIds);

        graph.Transitions.RemoveAll(t => !keep.Contains(t.FromStateId) || !keep.Contains(t.ToStateId));
        graph.States.RemoveAll(s => !keep.Contains(s.Id) && !s.Initial);
        graph.Metadata["states"] = graph.States.Count.ToString();
        graph.Metadata["transitions"] = graph.Transitions.Count.ToString();
    }
}
