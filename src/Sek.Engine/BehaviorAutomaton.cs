using Sek.Cord.Ast;
using Sek.Core.Model;

namespace Sek.Engine;

/// <summary>
/// Explores a Cord <em>behavior</em> machine (a scenario expressed with the behavior
/// algebra over abstract actions) into a finite transition graph.
///
/// The behavior is compiled to an NFA (Thompson construction), determinized by subset
/// construction, and emitted as an <see cref="ExplorationGraph"/>. Parallel operators
/// (<c>||</c> sync, <c>|||</c> interleave, <c>|?|</c> sync-interleave) are handled as
/// DFA products; permutation (<c>&amp;</c>) and loose sequence (<c>-&gt;</c>) are
/// desugared. The universal action <c>_</c> and negation range over the machine's
/// declared action alphabet.
/// </summary>
public sealed class BehaviorExplorer
{
    private readonly Func<string, Behavior?> _resolveMachine;
    private readonly HashSet<string> _alphabet;
    private int _nid;

    public BehaviorExplorer(Func<string, Behavior?> resolveMachine, IEnumerable<string> alphabet)
    {
        _resolveMachine = resolveMachine;
        _alphabet = new HashSet<string>(alphabet, StringComparer.Ordinal);
    }

    public ExplorationGraph Explore(string machineName, Behavior body)
    {
        var dfa = ToDfa(body);
        return ToGraph(machineName, dfa);
    }

    /// <summary>
    /// Compiles a Cord behavior into a steppable deterministic automaton over the action
    /// alphabet — used to <em>slice</em> a model program (a scenario restricts which action
    /// sequences the model may take). Machine references inside the behavior are resolved.
    /// </summary>
    public CompiledScenario Compile(Behavior body)
    {
        var dfa = ToDfa(body);
        var on = dfa.On.Select(d => new Dictionary<string, int>(d, StringComparer.Ordinal)).ToArray();
        return new CompiledScenario(dfa.Start, dfa.Accept.ToArray(), on);
    }

    /// <summary>A determinized scenario automaton over action labels.</summary>
    public sealed class CompiledScenario
    {
        private readonly bool[] _accept;
        private readonly Dictionary<string, int>[] _on;

        public CompiledScenario(int start, bool[] accept, Dictionary<string, int>[] on)
        {
            Start = start;
            _accept = accept;
            _on = on;
        }

        public int Start { get; }

        public bool IsAccepting(int state) => state >= 0 && state < _accept.Length && _accept[state];

        /// <summary>True if the scenario permits <paramref name="label"/> from <paramref name="state"/>,
        /// yielding the next scenario state.</summary>
        public bool TryStep(int state, string label, out int next)
        {
            next = -1;
            return state >= 0 && state < _on.Length && _on[state].TryGetValue(label, out next);
        }
    }

    // ---- NFA ----------------------------------------------------------------

    private sealed class NState
    {
        public int Id;
        public bool Accept;
        public readonly List<(string? Label, NState To)> Edges = new();
    }

    private sealed class Nfa
    {
        public NState Start = null!;
        public NState Accept = null!;
    }

    private NState New() => new() { Id = _nid++ };

    private Nfa Atom(string label)
    {
        var s = New();
        var a = New();
        a.Accept = true;
        s.Edges.Add((label, a));
        return new Nfa { Start = s, Accept = a };
    }

    private Nfa Empty()
    {
        var s = New();
        s.Accept = true;
        return new Nfa { Start = s, Accept = s };
    }

    private Nfa Build(Behavior b)
    {
        switch (b)
        {
            case InvocationBehavior inv:
                if (inv.Target == "_")
                {
                    return Atom("_");
                }

                if (inv.Negated)
                {
                    return Atom("!" + inv.Target);
                }

                var machine = _resolveMachine(inv.Target);
                return machine is not null ? Build(machine) : Atom(inv.Target);

            case GroupBehavior g:
                return Build(g.Inner);

            case PreconstraintBehavior pc:
                return Build(pc.Inner);

            case LetBehavior l:
                return Build(l.Inner);

            case SequenceBehavior seq:
                return Concat(seq.Items.Select(Build).ToList());

            case ChoiceBehavior ch:
                return Union(ch.Items.Select(Build).ToList());

            case RepetitionBehavior rep:
                return Repeat(rep);

            case LooseSequenceBehavior ls:
                // a -> b  ==  a ; _* ; b
                var parts = new List<Nfa>();
                for (var i = 0; i < ls.Items.Count; i++)
                {
                    parts.Add(Build(ls.Items[i]));
                    if (i < ls.Items.Count - 1)
                    {
                        parts.Add(Star(Atom("_")));
                    }
                }

                return Concat(parts);

            case PermutationBehavior perm when perm.Items.Count == 2:
                // (a & b) == (a; b) | (b; a)
                var ab = Concat(new List<Nfa> { Build(perm.Items[0]), Build(perm.Items[1]) });
                var ba = Concat(new List<Nfa> { Build(perm.Items[1]), Build(perm.Items[0]) });
                return Union(new List<Nfa> { ab, ba });

            default:
                return Empty();
        }
    }

    private Nfa Concat(List<Nfa> parts)
    {
        if (parts.Count == 0)
        {
            return Empty();
        }

        for (var i = 0; i < parts.Count - 1; i++)
        {
            parts[i].Accept.Accept = false;
            parts[i].Accept.Edges.Add((null, parts[i + 1].Start));
        }

        return new Nfa { Start = parts[0].Start, Accept = parts[^1].Accept };
    }

    private Nfa Union(List<Nfa> parts)
    {
        var s = New();
        var a = New();
        a.Accept = true;
        foreach (var p in parts)
        {
            s.Edges.Add((null, p.Start));
            p.Accept.Accept = false;
            p.Accept.Edges.Add((null, a));
        }

        return new Nfa { Start = s, Accept = a };
    }

    private Nfa Star(Nfa inner)
    {
        var s = New();
        var a = New();
        a.Accept = true;
        s.Edges.Add((null, inner.Start));
        s.Edges.Add((null, a));
        inner.Accept.Accept = false;
        inner.Accept.Edges.Add((null, inner.Start));
        inner.Accept.Edges.Add((null, a));
        return new Nfa { Start = s, Accept = a };
    }

    private Nfa Plus(Nfa inner)
    {
        var s = New();
        var a = New();
        a.Accept = true;
        s.Edges.Add((null, inner.Start));
        inner.Accept.Accept = false;
        inner.Accept.Edges.Add((null, inner.Start));
        inner.Accept.Edges.Add((null, a));
        return new Nfa { Start = s, Accept = a };
    }

    private Nfa Optional(Nfa inner)
    {
        var s = New();
        var a = New();
        a.Accept = true;
        s.Edges.Add((null, inner.Start));
        s.Edges.Add((null, a));
        inner.Accept.Accept = false;
        inner.Accept.Edges.Add((null, a));
        return new Nfa { Start = s, Accept = a };
    }

    private Nfa Repeat(RepetitionBehavior rep)
    {
        switch (rep.Op)
        {
            case "*":
                return Star(Build(rep.Inner));
            case "+":
                return Plus(Build(rep.Inner));
            case "?":
                return Optional(Build(rep.Inner));
            default: // "{}" bounded
                var min = rep.Min ?? 0;
                var parts = new List<Nfa>();
                for (var i = 0; i < min; i++) parts.Add(Build(rep.Inner));
                if (rep.Max is null)
                {
                    parts.Add(Star(Build(rep.Inner))); // {n,}
                }
                else
                {
                    for (var i = min; i < rep.Max.Value; i++) parts.Add(Optional(Build(rep.Inner))); // {n,m}
                }

                return parts.Count == 0 ? Empty() : Concat(parts);
        }
    }

    // ---- DFA ----------------------------------------------------------------

    private sealed class Dfa
    {
        public readonly List<bool> Accept = new();
        public readonly List<Dictionary<string, int>> On = new();
        public int Start;

        public int Add(bool accept)
        {
            Accept.Add(accept);
            On.Add(new Dictionary<string, int>(StringComparer.Ordinal));
            return Accept.Count - 1;
        }
    }

    private Dfa ToDfa(Behavior b)
    {
        switch (b)
        {
            case ParallelBehavior par when par.Items.Count == 2:
                return Product(ToDfa(par.Items[0]), ToDfa(par.Items[1]), par.Op);
            case GroupBehavior g:
                return ToDfa(g.Inner);
            default:
                return Subset(Build(b));
        }
    }

    private static HashSet<NState> Closure(IEnumerable<NState> states)
    {
        var set = new HashSet<NState>(states);
        var stack = new Stack<NState>(set);
        while (stack.Count > 0)
        {
            var s = stack.Pop();
            foreach (var (label, to) in s.Edges)
            {
                if (label is null && set.Add(to))
                {
                    stack.Push(to);
                }
            }
        }

        return set;
    }

    private Dfa Subset(Nfa nfa)
    {
        var dfa = new Dfa();
        var keyToId = new Dictionary<string, int>();
        var idToSet = new List<HashSet<NState>>();

        HashSet<NState> Start = Closure(new[] { nfa.Start });
        string Key(HashSet<NState> s) => string.Join(",", s.Select(x => x.Id).OrderBy(x => x));

        int GetOrAdd(HashSet<NState> s)
        {
            var k = Key(s);
            if (keyToId.TryGetValue(k, out var id))
            {
                return id;
            }

            id = dfa.Add(s.Any(x => x.Accept));
            keyToId[k] = id;
            idToSet.Add(s);
            return id;
        }

        dfa.Start = GetOrAdd(Start);
        var queue = new Queue<int>();
        queue.Enqueue(dfa.Start);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            var set = idToSet[id];
            foreach (var sym in _alphabet)
            {
                var move = new HashSet<NState>();
                foreach (var st in set)
                {
                    foreach (var (label, to) in st.Edges)
                    {
                        if (label is null)
                        {
                            continue;
                        }

                        var matches = label == sym
                                      || label == "_"
                                      || (label.StartsWith("!", StringComparison.Ordinal) && label[1..] != sym);
                        if (matches)
                        {
                            move.Add(to);
                        }
                    }
                }

                if (move.Count == 0)
                {
                    continue;
                }

                var closed = Closure(move);
                var before = keyToId.Count;
                var toId = GetOrAdd(closed);
                dfa.On[id][sym] = toId;
                if (keyToId.Count > before)
                {
                    queue.Enqueue(toId);
                }
            }
        }

        return dfa;
    }

    private static Dfa Product(Dfa l, Dfa r, string op)
    {
        var alphaL = l.On.SelectMany(d => d.Keys).ToHashSet(StringComparer.Ordinal);
        var alphaR = r.On.SelectMany(d => d.Keys).ToHashSet(StringComparer.Ordinal);
        var shared = new HashSet<string>(alphaL, StringComparer.Ordinal);
        shared.IntersectWith(alphaR);
        var union = new HashSet<string>(alphaL, StringComparer.Ordinal);
        union.UnionWith(alphaR);

        var dfa = new Dfa();
        var keyToId = new Dictionary<string, int>();
        var idToPair = new List<(int L, int R)>();

        int GetOrAdd(int li, int ri)
        {
            var k = $"{li}:{ri}";
            if (keyToId.TryGetValue(k, out var id))
            {
                return id;
            }

            id = dfa.Add(l.Accept[li] && r.Accept[ri]);
            keyToId[k] = id;
            idToPair.Add((li, ri));
            return id;
        }

        dfa.Start = GetOrAdd(l.Start, r.Start);
        var queue = new Queue<int>();
        queue.Enqueue(dfa.Start);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            var (li, ri) = idToPair[id];

            foreach (var sym in union)
            {
                var lHas = l.On[li].TryGetValue(sym, out var ln);
                var rHas = r.On[ri].TryGetValue(sym, out var rn);
                var mustSync = op switch
                {
                    "sync" => true,
                    "interleave" => false,
                    "syncinterleave" => shared.Contains(sym),
                    _ => true,
                };

                int? nextL = null, nextR = null;
                if (mustSync)
                {
                    if (lHas && rHas) { nextL = ln; nextR = rn; }
                    else { continue; }

                    AddEdge(id, sym, nextL.Value, nextR.Value);
                }
                else
                {
                    // interleave: advance whichever side can take the symbol
                    if (lHas) AddEdge(id, sym, ln, ri);
                    if (rHas) AddEdge(id, sym, li, rn);
                }
            }
        }

        void AddEdge(int fromId, string sym, int li2, int ri2)
        {
            var before = keyToId.Count;
            var toId = GetOrAdd(li2, ri2);
            dfa.On[fromId][sym] = toId;
            if (keyToId.Count > before)
            {
                queue.Enqueue(toId);
            }
        }

        return dfa;
    }

    private static ExplorationGraph ToGraph(string machine, Dfa dfa)
    {
        var graph = new ExplorationGraph { Machine = machine };
        for (var i = 0; i < dfa.Accept.Count; i++)
        {
            graph.States.Add(new ModelState("S" + i, "S" + i, Label: null, Accepting: dfa.Accept[i], Initial: i == dfa.Start));
        }

        graph.InitialStateId = "S" + dfa.Start;

        for (var i = 0; i < dfa.On.Count; i++)
        {
            foreach (var (sym, to) in dfa.On[i].OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                graph.Transitions.Add(new Transition("S" + i, new ActionInvocation(sym, Array.Empty<string>()), "S" + to));
            }
        }

        graph.Metadata["states"] = graph.States.Count.ToString();
        graph.Metadata["transitions"] = graph.Transitions.Count.ToString();
        graph.Metadata["accepting"] = graph.States.Count(s => s.Accepting).ToString();
        graph.Metadata["mode"] = "behavior";
        return graph;
    }
}
