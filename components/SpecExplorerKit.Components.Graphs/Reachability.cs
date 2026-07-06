namespace SpecExplorerKit.Components.Graphs;

/// <summary>
/// Generic directed-graph reachability over abstract edges. An edge is an ordered pair
/// <c>(From, To)</c>; nodes may be any type with an <see cref="IEqualityComparer{T}"/>.
/// </summary>
/// <remarks>
/// Domain-free (EngLoopKit component pattern): it knows nothing about SpecExplorerKit's
/// exploration graph, states, or transitions. Callers project their own graph onto
/// <c>(From, To)</c> edges and interpret the returned node sets. Both traversals include the
/// seed nodes in the result.
/// </remarks>
public static class Reachability
{
    /// <summary>
    /// Backward reachability: every node from which at least one <paramref name="seeds"/> node is
    /// reachable by following edges forward (i.e. the set of nodes that can <em>reach</em> a seed),
    /// including the seeds themselves.
    /// </summary>
    public static HashSet<TNode> Backward<TNode>(
        IEnumerable<(TNode From, TNode To)> edges,
        IEnumerable<TNode> seeds,
        IEqualityComparer<TNode>? comparer = null)
        where TNode : notnull
    {
        comparer ??= EqualityComparer<TNode>.Default;
        var edgeList = edges as IReadOnlyList<(TNode From, TNode To)> ?? edges.ToList();
        var reached = new HashSet<TNode>(seeds, comparer);

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (from, to) in edgeList)
            {
                if (reached.Contains(to) && reached.Add(from))
                {
                    changed = true;
                }
            }
        }

        return reached;
    }

    /// <summary>
    /// Forward reachability: every node reachable from <paramref name="seeds"/> by following edges
    /// forward, including the seeds themselves.
    /// </summary>
    public static HashSet<TNode> Forward<TNode>(
        IEnumerable<(TNode From, TNode To)> edges,
        IEnumerable<TNode> seeds,
        IEqualityComparer<TNode>? comparer = null)
        where TNode : notnull
    {
        comparer ??= EqualityComparer<TNode>.Default;
        var outAdj = new Dictionary<TNode, List<TNode>>(comparer);
        foreach (var (from, to) in edges)
        {
            if (!outAdj.TryGetValue(from, out var list))
            {
                list = new List<TNode>();
                outAdj[from] = list;
            }

            list.Add(to);
        }

        var reached = new HashSet<TNode>(seeds, comparer);
        var stack = new Stack<TNode>(reached);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!outAdj.TryGetValue(node, out var outs)) continue;
            foreach (var next in outs)
            {
                if (reached.Add(next)) stack.Push(next);
            }
        }

        return reached;
    }
}
