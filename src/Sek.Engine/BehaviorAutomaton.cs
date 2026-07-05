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
        foreach (var sym in CollectSymbols(body, new HashSet<string>(StringComparer.Ordinal)))
        {
            _alphabet.Add(sym);
        }

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
        foreach (var sym in CollectSymbols(body, new HashSet<string>(StringComparer.Ordinal)))
        {
            _alphabet.Add(sym);
        }

        // Return-bindings: map each producing atom's symbol to the variable it binds
        // (`Producer(args) / var`), and collect the variable names so a consumer's argument that
        // references one is matched against the captured value during slicing.
        var returnBindings = new Dictionary<string, string>(StringComparer.Ordinal);
        var bindingVars = new HashSet<string>(StringComparer.Ordinal);
        CollectReturnBindings(body, new HashSet<string>(StringComparer.Ordinal), returnBindings, bindingVars);

        return new CompiledScenario(ToDfa(body), ContainsFail(body, new HashSet<string>(StringComparer.Ordinal)), returnBindings, bindingVars);
    }

    /// <summary>Walks the behavior collecting return-binding metadata: <c>symbol → bound
    /// variable</c> for each <c>Action(args) / var</c>, and the set of bound variable names.</summary>
    private void CollectReturnBindings(Behavior? b, HashSet<string> seenMachines, Dictionary<string, string> map, HashSet<string> vars)
    {
        switch (b)
        {
            case null: return;
            case InvocationBehavior inv:
                if (inv.ReturnBinding is { Length: > 0 } v)
                {
                    map[SymbolOf(inv)] = v;
                    vars.Add(v);
                }

                var m = _resolveMachine(inv.Target);
                if (m is not null && seenMachines.Add(inv.Target)) CollectReturnBindings(m, seenMachines, map, vars);
                return;
            case SequenceBehavior s: foreach (var i in s.Items) CollectReturnBindings(i, seenMachines, map, vars); return;
            case ChoiceBehavior c: foreach (var i in c.Items) CollectReturnBindings(i, seenMachines, map, vars); return;
            case ParallelBehavior p: foreach (var i in p.Items) CollectReturnBindings(i, seenMachines, map, vars); return;
            case PermutationBehavior pm: foreach (var i in pm.Items) CollectReturnBindings(i, seenMachines, map, vars); return;
            case LooseSequenceBehavior ls: foreach (var i in ls.Items) CollectReturnBindings(i, seenMachines, map, vars); return;
            case RepetitionBehavior rep: CollectReturnBindings(rep.Inner, seenMachines, map, vars); return;
            case GroupBehavior g: CollectReturnBindings(g.Inner, seenMachines, map, vars); return;
            case PreconstraintBehavior pc: CollectReturnBindings(pc.Inner, seenMachines, map, vars); return;
            case FailBehavior fb: CollectReturnBindings(fb.Inner, seenMachines, map, vars); return;
            case LetBehavior l: CollectReturnBindings(l.Inner, seenMachines, map, vars); return;
            case BindBehavior bd: CollectReturnBindings(bd.Inner, seenMachines, map, vars); return;
        }
    }

    /// <summary>Whether the behavior contains a <c>: fail</c> annotation (recursing machine
    /// references) — i.e. it is a model-checking scenario.</summary>
    private bool ContainsFail(Behavior? b, HashSet<string> seenMachines)
    {
        switch (b)
        {
            case null: return false;
            case FailBehavior: return true;
            case InvocationBehavior inv:
                var m = _resolveMachine(inv.Target);
                return m is not null && seenMachines.Add(inv.Target) && ContainsFail(m, seenMachines);
            case SequenceBehavior s: return s.Items.Any(i => ContainsFail(i, seenMachines));
            case ChoiceBehavior c: return c.Items.Any(i => ContainsFail(i, seenMachines));
            case ParallelBehavior p: return p.Items.Any(i => ContainsFail(i, seenMachines));
            case PermutationBehavior pm: return pm.Items.Any(i => ContainsFail(i, seenMachines));
            case LooseSequenceBehavior ls: return ls.Items.Any(i => ContainsFail(i, seenMachines));
            case RepetitionBehavior rep: return ContainsFail(rep.Inner, seenMachines);
            case GroupBehavior g: return ContainsFail(g.Inner, seenMachines);
            case PreconstraintBehavior pc: return ContainsFail(pc.Inner, seenMachines);
            case LetBehavior l: return ContainsFail(l.Inner, seenMachines);
            case BindBehavior bd: return ContainsFail(bd.Inner, seenMachines);
            default: return false;
        }
    }

    /// <summary>The scenario symbol for an invocation: the bare action label, or
    /// <c>label(arg1,arg2)</c> when the scenario pins argument values (a bare label matches
    /// any arguments; a pinned symbol matches only those argument values).</summary>
    public static string SymbolOf(InvocationBehavior inv)
    {
        if (inv.Args is null || inv.Args.Count == 0) return inv.Target;
        return inv.Target + "(" + string.Join(",", inv.Args.Select(NormArg)) + ")";
    }

    /// <summary>Normalizes an argument token for matching: strips quotes and any type prefix
    /// (e.g. <c>ShareType.DISK</c> -&gt; <c>DISK</c>) so it aligns with a stringified value.</summary>
    public static string NormArg(string a)
    {
        a = a.Trim();
        if (a.Length >= 2 && a[0] == '"' && a[^1] == '"') a = a[1..^1];
        var dot = a.LastIndexOf('.');
        return dot >= 0 ? a[(dot + 1)..] : a;
    }

    private IEnumerable<string> CollectSymbols(Behavior? b, HashSet<string> seenMachines)
    {
        switch (b)
        {
            case null:
                yield break;
            case InvocationBehavior inv:
                if (inv.Target == "_" || inv.Negated) yield break;
                var m = _resolveMachine(inv.Target);
                if (m is not null)
                {
                    if (seenMachines.Add(inv.Target))
                        foreach (var s in CollectSymbols(m, seenMachines)) yield return s;
                }
                else if (inv.Args is { Count: > 0 })
                {
                    yield return SymbolOf(inv);
                }

                yield break;
            case SequenceBehavior s: foreach (var i in s.Items) foreach (var x in CollectSymbols(i, seenMachines)) yield return x; yield break;
            case ChoiceBehavior c: foreach (var i in c.Items) foreach (var x in CollectSymbols(i, seenMachines)) yield return x; yield break;
            case ParallelBehavior p: foreach (var i in p.Items) foreach (var x in CollectSymbols(i, seenMachines)) yield return x; yield break;
            case PermutationBehavior pm: foreach (var i in pm.Items) foreach (var x in CollectSymbols(i, seenMachines)) yield return x; yield break;
            case LooseSequenceBehavior ls: foreach (var i in ls.Items) foreach (var x in CollectSymbols(i, seenMachines)) yield return x; yield break;
            case RepetitionBehavior rep: foreach (var x in CollectSymbols(rep.Inner, seenMachines)) yield return x; yield break;
            case GroupBehavior g: foreach (var x in CollectSymbols(g.Inner, seenMachines)) yield return x; yield break;
            case PreconstraintBehavior pc: foreach (var x in CollectSymbols(pc.Inner, seenMachines)) yield return x; yield break;
            case FailBehavior fb: foreach (var x in CollectSymbols(fb.Inner, seenMachines)) yield return x; yield break;
            case LetBehavior l: foreach (var x in CollectSymbols(l.Inner, seenMachines)) yield return x; yield break;
            case BindBehavior bd: foreach (var x in CollectSymbols(bd.Inner, seenMachines)) yield return x; yield break;
        }
    }

    /// <summary>A lazily-determinized scenario automaton over action labels (states and
    /// transitions are computed on demand from the underlying NFA / product).</summary>
    public sealed class CompiledScenario
    {
        private readonly IDfa _dfa;
        private readonly IReadOnlyDictionary<string, string> _returnBindings;
        private readonly IReadOnlySet<string> _bindingVars;

        internal CompiledScenario(IDfa dfa, bool hasFail)
            : this(dfa, hasFail, new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal))
        {
        }

        internal CompiledScenario(IDfa dfa, bool hasFail, IReadOnlyDictionary<string, string> returnBindings, IReadOnlySet<string> bindingVars)
        {
            _dfa = dfa;
            HasFailStates = hasFail;
            Start = dfa.Start;
            _returnBindings = returnBindings;
            _bindingVars = bindingVars;
        }

        public int Start { get; }

        /// <summary>True when the scenario uses return-bindings (<c>Action(args) / var</c>).</summary>
        public bool HasReturnBindings => _returnBindings.Count > 0;

        public bool IsAccepting(int state) => state >= 0 && _dfa.Accept(state);

        /// <summary>True if <paramref name="state"/> is a model-checking failure state
        /// (reached a <c>: fail</c> annotation).</summary>
        public bool IsFail(int state) => state >= 0 && _dfa.Fail(state);

        /// <summary>True if the scenario has any failure states (a model-checking machine).</summary>
        public bool HasFailStates { get; }

        /// <summary>True if the scenario permits <paramref name="label"/> from <paramref name="state"/>,
        /// yielding the next scenario state.</summary>
        public bool TryStep(int state, string label, out int next)
        {
            next = -1;
            return state >= 0 && _dfa.On(state).TryGetValue(label, out next);
        }

        /// <summary>True if the scenario permits the action <paramref name="bareLabel"/> from
        /// <paramref name="state"/> in any form — the bare label (any arguments) or an
        /// argument-pinned form <c>bareLabel(...)</c>.</summary>
        public bool Permits(int state, string bareLabel)
        {
            if (state < 0) return false;
            var on = _dfa.On(state);
            if (on.ContainsKey(bareLabel)) return true;
            var prefix = bareLabel + "(";
            foreach (var k in on.Keys)
            {
                if (k.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>Steps an argument-pinned action, matching each scenario argument pattern
        /// against the transition's concrete (normalized) arguments positionally. A pattern
        /// argument of <c>_</c> is a wildcard that matches any value; other pattern arguments
        /// must match exactly. Returns the next scenario state on the first matching pattern.</summary>
        public bool TryStepArgs(int state, string bareLabel, IReadOnlyList<string> concreteArgs, out int next)
        {
            next = -1;
            if (state < 0) return false;
            var prefix = bareLabel + "(";
            foreach (var kv in _dfa.On(state))
            {
                var key = kv.Key;
                if (!key.StartsWith(prefix, StringComparison.Ordinal) || !key.EndsWith(")", StringComparison.Ordinal))
                {
                    continue;
                }

                var inside = key.Substring(prefix.Length, key.Length - prefix.Length - 1);
                var pat = inside.Length == 0 ? Array.Empty<string>() : inside.Split(',');
                if (pat.Length != concreteArgs.Count)
                {
                    continue;
                }

                var ok = true;
                for (var i = 0; i < pat.Length; i++)
                {
                    if (pat[i] == "_") continue; // wildcard
                    if (!string.Equals(pat[i], concreteArgs[i], StringComparison.Ordinal)) { ok = false; break; }
                }

                if (ok) { next = kv.Value; return true; }
            }

            return false;
        }

        /// <summary>Steps an action honouring return-bindings: a scenario argument token that names
        /// a bound variable matches the value captured for it (via <paramref name="env"/>) — or is a
        /// wildcard while still unbound. Also matches the bare label (any args). On success, reports
        /// the next state and, when the taken atom is a producer (<c>Action(args) / var</c>), the
        /// variable <paramref name="boundVar"/> whose value the caller should capture.</summary>
        public bool TryStepBinding(int state, string bareLabel, IReadOnlyList<string> concreteArgs, IReadOnlyDictionary<string, string> env, out int next, out string? boundVar)
        {
            next = -1;
            boundVar = null;
            if (state < 0) return false;
            var on = _dfa.On(state);
            var prefix = bareLabel + "(";

            foreach (var kv in on)
            {
                var key = kv.Key;
                if (key.StartsWith(prefix, StringComparison.Ordinal) && key.EndsWith(")", StringComparison.Ordinal))
                {
                    var inside = key.Substring(prefix.Length, key.Length - prefix.Length - 1);
                    var pat = inside.Length == 0 ? Array.Empty<string>() : inside.Split(',');
                    if (pat.Length != concreteArgs.Count) continue;

                    var ok = true;
                    for (var i = 0; i < pat.Length; i++)
                    {
                        if (pat[i] == "_") continue;                          // wildcard
                        if (_bindingVars.Contains(pat[i]))                    // binding-var reference
                        {
                            if (env.TryGetValue(pat[i], out var bound) && !string.Equals(bound, concreteArgs[i], StringComparison.Ordinal)) { ok = false; break; }
                            continue; // unbound var acts as a wildcard
                        }

                        if (!string.Equals(pat[i], concreteArgs[i], StringComparison.Ordinal)) { ok = false; break; }
                    }

                    if (ok) { next = kv.Value; boundVar = _returnBindings.GetValueOrDefault(key); return true; }
                }
            }

            // Bare label (a producer with no pinned args, e.g. `Producer() / h`).
            if (on.TryGetValue(bareLabel, out var bareNext))
            {
                next = bareNext;
                boundVar = _returnBindings.GetValueOrDefault(bareLabel);
                return true;
            }

            return false;
        }
        /// <paramref name="state"/> (each a normalized token array; <c>_</c> = wildcard). Used to
        /// feed scenario-supplied argument values into parameter generation during slicing.</summary>
        public IEnumerable<string[]> ArgPatterns(int state, string bareLabel)
        {
            if (state < 0) yield break;
            var prefix = bareLabel + "(";
            foreach (var kv in _dfa.On(state))
            {
                var key = kv.Key;
                if (!key.StartsWith(prefix, StringComparison.Ordinal) || !key.EndsWith(")", StringComparison.Ordinal))
                {
                    continue;
                }

                var inside = key.Substring(prefix.Length, key.Length - prefix.Length - 1);
                yield return inside.Length == 0 ? Array.Empty<string>() : inside.Split(',');
            }
        }
    }

    // ---- NFA ----------------------------------------------------------------

    private sealed class NState
    {
        public int Id;
        public bool Accept;
        public bool Fail;
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
                return machine is not null ? Build(machine) : Atom(SymbolOf(inv));

            case GroupBehavior g:
                return Build(g.Inner);

            case PreconstraintBehavior pc:
                return Build(pc.Inner);

            case FailBehavior fb:
            {
                var n = Build(fb.Inner);
                n.Accept.Fail = true;
                return n;
            }

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

    // ---- DFA (lazy determinization) ----------------------------------------
    // Determinization is performed on demand: DFA states (NFA subsets, or products of
    // sub-DFAs) are materialized and memoized only as a query reaches them, never eagerly
    // enumerated. This keeps an unanchored scenario with many alternatives — whose full DFA is
    // exponential — costing only what the bounded model exploration actually visits.

    private static bool Matches(string label, string sym) =>
        label == sym
        || label == "_"
        || (label.StartsWith("!", StringComparison.Ordinal) && label[1..] != sym);

    /// <summary>A lazily-determinized automaton over action labels.</summary>
    internal interface IDfa
    {
        int Start { get; }
        bool Accept(int state);
        bool Fail(int state);
        IReadOnlyDictionary<string, int> On(int state);
    }

    private IDfa ToDfa(Behavior b) => b switch
    {
        ParallelBehavior par when par.Items.Count == 2 => new ProductDfa(ToDfa(par.Items[0]), ToDfa(par.Items[1]), par.Op),
        GroupBehavior g => ToDfa(g.Inner),
        _ => new SubsetDfa(Build(b).Start, _alphabet),
    };

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

    /// <summary>Lazy subset construction over an NFA: each DFA state is an NFA-state subset,
    /// materialized (with its outgoing transitions) on first query and memoized.</summary>
    private sealed class SubsetDfa : IDfa
    {
        private readonly HashSet<string> _alphabet;
        private readonly List<HashSet<NState>> _sets = new();
        private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
        private readonly Dictionary<int, IReadOnlyDictionary<string, int>> _on = new();

        public SubsetDfa(NState nfaStart, HashSet<string> alphabet)
        {
            _alphabet = alphabet;
            Start = GetOrAdd(Closure(new[] { nfaStart }));
        }

        public int Start { get; }

        private int GetOrAdd(HashSet<NState> s)
        {
            var key = string.Join(",", s.Select(x => x.Id).OrderBy(x => x));
            if (_ids.TryGetValue(key, out var id)) return id;
            id = _sets.Count;
            _ids[key] = id;
            _sets.Add(s);
            return id;
        }

        public bool Accept(int state) => _sets[state].Any(x => x.Accept);

        public bool Fail(int state) => _sets[state].Any(x => x.Fail);

        public IReadOnlyDictionary<string, int> On(int state)
        {
            if (_on.TryGetValue(state, out var cached)) return cached;

            var set = _sets[state];
            var d = new Dictionary<string, int>(StringComparer.Ordinal);

            // The concrete edge labels leaving this subset (bare, pinned, or negated).
            var concrete = set.SelectMany(st => st.Edges)
                .Where(e => e.Label is not null && e.Label != "_" && !e.Label.StartsWith("!", StringComparison.Ordinal))
                .Select(e => e.Label!);

            // A universal `_` or negated `!x` edge matches every *model action*, i.e. the bare
            // (non-argument-pinned) alphabet symbols — not the scenario's own pinned symbols,
            // which are only reached by their explicit atoms. Restricting `_` this way avoids
            // materializing dead states for argument-pinned symbols the model never emits.
            var hasUniversal = set.Any(st => st.Edges.Any(e =>
                e.Label == "_" || (e.Label is not null && e.Label.StartsWith("!", StringComparison.Ordinal))));
            IEnumerable<string> symbols = hasUniversal
                ? _alphabet.Where(s => !s.Contains('(')).Concat(concrete).Distinct(StringComparer.Ordinal)
                : concrete.Distinct(StringComparer.Ordinal);

            foreach (var sym in symbols)
            {
                var move = new HashSet<NState>();
                foreach (var st in set)
                {
                    foreach (var (label, to) in st.Edges)
                    {
                        if (label is not null && Matches(label, sym)) move.Add(to);
                    }
                }

                if (move.Count > 0) d[sym] = GetOrAdd(Closure(move));
            }

            _on[state] = d;
            return d;
        }
    }

    /// <summary>Lazy product of two DFAs for the parallel operators (sync / interleave /
    /// sync-interleave). Product states (pairs of sub-DFA states) are created on demand.</summary>
    private sealed class ProductDfa : IDfa
    {
        private readonly IDfa _l;
        private readonly IDfa _r;
        private readonly string _op;
        private readonly HashSet<string>? _syncInterleaveShared;
        private readonly List<(int L, int R)> _pairs = new();
        private readonly Dictionary<(int, int), int> _ids = new();
        private readonly Dictionary<int, IReadOnlyDictionary<string, int>> _on = new();

        public ProductDfa(IDfa l, IDfa r, string op)
        {
            _l = l;
            _r = r;
            _op = op;
            // Sync-interleave (`|?|`) synchronizes on the shared *signature* — the full alphabets
            // of both sides — not per-state outgoing symbols, so compute it up front (the sides
            // are small; this is not used for `sync`/`interleave`).
            if (op == "syncinterleave")
            {
                var la = FullAlphabet(l);
                la.IntersectWith(FullAlphabet(r));
                _syncInterleaveShared = la;
            }

            Start = GetOrAdd(l.Start, r.Start);
        }

        public int Start { get; }

        private int GetOrAdd(int li, int ri)
        {
            if (_ids.TryGetValue((li, ri), out var id)) return id;
            id = _pairs.Count;
            _ids[(li, ri)] = id;
            _pairs.Add((li, ri));
            return id;
        }

        public bool Accept(int state) { var (li, ri) = _pairs[state]; return _l.Accept(li) && _r.Accept(ri); }

        public bool Fail(int state) { var (li, ri) = _pairs[state]; return _l.Fail(li) || _r.Fail(ri); }

        public IReadOnlyDictionary<string, int> On(int state)
        {
            if (_on.TryGetValue(state, out var cached)) return cached;

            var (li, ri) = _pairs[state];
            var lon = _l.On(li);
            var ron = _r.On(ri);
            var union = new HashSet<string>(lon.Keys, StringComparer.Ordinal);
            union.UnionWith(ron.Keys);

            var d = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var sym in union)
            {
                // A bare label transition matches any argument-pinned form of it, so a scenario
                // over bare labels syncs with one that pins arguments (`CreateResponse` syncs
                // with `CreateResponse(1)`), yielding the pinned symbol.
                var lHas = SideHas(lon, sym, out var ln);
                var rHas = SideHas(ron, sym, out var rn);
                var mustSync = _op switch
                {
                    "sync" => true,
                    "interleave" => false,
                    "syncinterleave" => _syncInterleaveShared!.Contains(sym),
                    _ => true,
                };

                if (mustSync)
                {
                    if (lHas && rHas) d[sym] = GetOrAdd(ln, rn);
                }
                else
                {
                    if (lHas) d[sym] = GetOrAdd(ln, ri);
                    if (rHas) d[sym] = GetOrAdd(li, rn);
                }
            }

            _on[state] = d;
            return d;
        }
    }

    /// <summary>Fully materializes a (lazy) DFA and returns the set of all symbols on its
    /// transitions — its action signature. Bounded by the DFA size.</summary>
    private static HashSet<string> FullAlphabet(IDfa dfa)
    {
        var syms = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<int> { dfa.Start };
        var queue = new Queue<int>();
        queue.Enqueue(dfa.Start);
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            foreach (var (k, to) in dfa.On(s))
            {
                syms.Add(k);
                if (seen.Add(to)) queue.Enqueue(to);
            }
        }

        return syms;
    }

    /// <summary>Looks up a transition for <paramref name="sym"/>, treating a bare label
    /// transition as matching an argument-pinned symbol (<c>X</c> matches <c>X(args)</c>).</summary>
    private static bool SideHas(IReadOnlyDictionary<string, int> on, string sym, out int next)
    {
        if (on.TryGetValue(sym, out next)) return true;
        var paren = sym.IndexOf('(');
        if (paren > 0 && on.TryGetValue(sym[..paren], out next)) return true;
        return false;
    }

    private static ExplorationGraph ToGraph(string machine, IDfa dfa)
    {
        var graph = new ExplorationGraph { Machine = machine };
        graph.InitialStateId = "S" + dfa.Start;

        var visited = new HashSet<int> { dfa.Start };
        var queue = new Queue<int>();
        queue.Enqueue(dfa.Start);
        var states = new List<int>();
        var edges = new List<(int From, string Sym, int To)>();

        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            states.Add(s);
            foreach (var (sym, to) in dfa.On(s).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                edges.Add((s, sym, to));
                if (visited.Add(to)) queue.Enqueue(to);
            }
        }

        foreach (var s in states.OrderBy(x => x))
        {
            graph.States.Add(new ModelState("S" + s, "S" + s, Label: null, Accepting: dfa.Accept(s), Initial: s == dfa.Start));
        }

        foreach (var (from, sym, to) in edges)
        {
            graph.Transitions.Add(new Transition("S" + from, new ActionInvocation(sym, Array.Empty<string>()), "S" + to));
        }

        graph.Metadata["states"] = graph.States.Count.ToString();
        graph.Metadata["transitions"] = graph.Transitions.Count.ToString();
        graph.Metadata["accepting"] = graph.States.Count(s => s.Accepting).ToString();
        graph.Metadata["mode"] = "behavior";
        return graph;
    }
}
