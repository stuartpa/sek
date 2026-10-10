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
    private readonly ExplorationOptions _options;
    private readonly ExplorationBudget _budget;
    private int _nid;
    private int _walkDepth;
    private int _scanRemaining;

    public BehaviorExplorer(Func<string, Behavior?> resolveMachine, IEnumerable<string> alphabet)
        : this(resolveMachine, alphabet, new ExplorationOptions())
    {
    }

    public BehaviorExplorer(Func<string, Behavior?> resolveMachine, IEnumerable<string> alphabet, ExplorationOptions options)
    {
        _resolveMachine = resolveMachine;
        _options = options;
        _budget = options.Budget ??= new ExplorationBudget(options);
        _alphabet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in alphabet)
        {
            Work();
            _alphabet.Add(symbol);
        }
    }

    private void Work([System.Runtime.CompilerServices.CallerMemberName] string operation = "")
    {
        if (!_budget.TryCandidate())
            throw new InvalidOperationException($"INCOMPLETE behavior preprocessing: {_budget.TerminationReason} ({operation}; NFA nodes={_nid}; graph states={_budget.States})");
    }

    private void CheckBudget()
    {
        if (!_budget.Check())
            throw new InvalidOperationException($"INCOMPLETE behavior preprocessing: {_budget.TerminationReason}");
    }

    private void Scan()
    {
        CheckBudget();
        if (_scanRemaining == 0)
        {
            Work();
            _scanRemaining = 31;
        }
        else _scanRemaining--;
    }

    private WalkScope EnterWalk(bool candidate = true)
    {
        if (candidate) Work();
        else CheckBudget();
        if (_walkDepth >= 256)
        {
            _budget.Stop("behavior-recursion-limit");
            throw new InvalidOperationException($"INCOMPLETE behavior preprocessing: {_budget.TerminationReason}");
        }

        _walkDepth++;
        return new WalkScope(this);
    }

    private readonly struct WalkScope(BehaviorExplorer owner) : IDisposable
    {
        public void Dispose() => owner._walkDepth--;
    }

    public ExplorationGraph Explore(string machineName, Behavior body)
    {
        Work();
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
        Work();
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
        using var walk = EnterWalk();
        switch (b)
        {
            case null: return;
            case InvocationBehavior inv:
                if (inv.ReturnBinding is { Length: > 0 } v)
                {
                    map[CheckedSymbolOf(inv)] = v;
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
        using var walk = EnterWalk();
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

    private string CheckedSymbolOf(InvocationBehavior invocation)
    {
        Work();
        if (invocation.Args is null || invocation.Args.Count == 0) return invocation.Target;
        return invocation.Target + "(" + string.Join(",", invocation.Args.Select(argument =>
        {
            Scan();
            return NormArg(argument);
        })) + ")";
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
        using var walk = EnterWalk();
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
                    yield return CheckedSymbolOf(inv);
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

        public bool IsAccepting(int state)
        {
            _dfa.Check();
            return state >= 0 && _dfa.Accept(state);
        }

        /// <summary>True if <paramref name="state"/> is a model-checking failure state
        /// (reached a <c>: fail</c> annotation).</summary>
        public bool IsFail(int state)
        {
            _dfa.Check();
            return state >= 0 && _dfa.Fail(state);
        }

        /// <summary>True if the scenario has any failure states (a model-checking machine).</summary>
        public bool HasFailStates { get; }

        /// <summary>True if the scenario permits <paramref name="label"/> from <paramref name="state"/>,
        /// yielding the next scenario state.</summary>
        public bool TryStep(int state, string label, out int next)
        {
            _dfa.Check();
            next = -1;
            return state >= 0 && _dfa.On(state).TryGetValue(label, out next);
        }

        /// <summary>True if the scenario permits the action <paramref name="bareLabel"/> from
        /// <paramref name="state"/> in any form — the bare label (any arguments) or an
        /// argument-pinned form <c>bareLabel(...)</c>.</summary>
        public bool Permits(int state, string bareLabel)
        {
            _dfa.Check();
            if (state < 0) return false;
            var on = _dfa.On(state);
            if (on.ContainsKey(bareLabel)) return true;
            var prefix = bareLabel + "(";
            foreach (var k in on.Keys)
            {
                _dfa.Check();
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
            _dfa.Check();
            next = -1;
            if (state < 0) return false;
            var prefix = bareLabel + "(";
            foreach (var kv in _dfa.On(state))
            {
                _dfa.Check();
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
                    _dfa.Check();
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
            _dfa.Check();
            next = -1;
            boundVar = null;
            if (state < 0) return false;
            var on = _dfa.On(state);
            var prefix = bareLabel + "(";

            foreach (var kv in on)
            {
                _dfa.Check();
                var key = kv.Key;
                if (key.StartsWith(prefix, StringComparison.Ordinal) && key.EndsWith(")", StringComparison.Ordinal))
                {
                    var inside = key.Substring(prefix.Length, key.Length - prefix.Length - 1);
                    var pat = inside.Length == 0 ? Array.Empty<string>() : inside.Split(',');
                    if (pat.Length != concreteArgs.Count) continue;

                    var ok = true;
                    for (var i = 0; i < pat.Length; i++)
                    {
                        _dfa.Check();
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
            _dfa.Check();
            if (state < 0) yield break;
            var prefix = bareLabel + "(";
            foreach (var kv in _dfa.On(state))
            {
                _dfa.Check();
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

    private NState New()
    {
        Work();
        return new NState { Id = _nid++ };
    }

    private void AddEdge(NState from, string? label, NState to)
    {
        Work();
        from.Edges.Add((label, to));
    }

    private Nfa Atom(string label)
    {
        var s = New();
        var a = New();
        a.Accept = true;
        AddEdge(s, label, a);
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
        using var walk = EnterWalk();
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
                return machine is not null ? Build(machine) : Atom(CheckedSymbolOf(inv));

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

            case BindBehavior bind:
                return Build(bind.Inner);

            case ParallelBehavior parallel when parallel.Items.Count <= 1:
                return Concat(parallel.Items.Select(Build).ToList());

            case PermutationBehavior permutation when permutation.Items.Count <= 1:
                return Concat(permutation.Items.Select(Build).ToList());

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
        Work();
        if (parts.Count == 0)
        {
            return Empty();
        }

        for (var i = 0; i < parts.Count - 1; i++)
        {
            parts[i].Accept.Accept = false;
            AddEdge(parts[i].Accept, null, parts[i + 1].Start);
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
            AddEdge(s, null, p.Start);
            p.Accept.Accept = false;
            AddEdge(p.Accept, null, a);
        }

        return new Nfa { Start = s, Accept = a };
    }

    private Nfa Star(Nfa inner)
    {
        var s = New();
        var a = New();
        a.Accept = true;
        AddEdge(s, null, inner.Start);
        AddEdge(s, null, a);
        inner.Accept.Accept = false;
        AddEdge(inner.Accept, null, inner.Start);
        AddEdge(inner.Accept, null, a);
        return new Nfa { Start = s, Accept = a };
    }

    private Nfa Plus(Nfa inner)
    {
        var s = New();
        var a = New();
        a.Accept = true;
        AddEdge(s, null, inner.Start);
        inner.Accept.Accept = false;
        AddEdge(inner.Accept, null, inner.Start);
        AddEdge(inner.Accept, null, a);
        return new Nfa { Start = s, Accept = a };
    }

    private Nfa Optional(Nfa inner)
    {
        var s = New();
        var a = New();
        a.Accept = true;
        AddEdge(s, null, inner.Start);
        AddEdge(s, null, a);
        inner.Accept.Accept = false;
        AddEdge(inner.Accept, null, a);
        return new Nfa { Start = s, Accept = a };
    }

    private Nfa Repeat(RepetitionBehavior rep)
    {
        Work();
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

    /// <summary>A lazily-determinized automaton over action labels.</summary>
    internal interface IDfa
    {
        int Start { get; }
        void Check();
        bool Accept(int state);
        bool Fail(int state);
        IReadOnlyDictionary<string, int> On(int state);
    }

    private IDfa ToDfa(Behavior b)
    {
        using var walk = EnterWalk();
        return b switch
        {
            ParallelBehavior par when par.Items.Count == 2 => new ProductDfa(this, ToDfa(par.Items[0]), ToDfa(par.Items[1]), par.Op),
            GroupBehavior g => ToDfa(g.Inner),
            _ => new SubsetDfa(this, Build(b).Start, _alphabet),
        };
    }

    private HashSet<NState> Closure(IEnumerable<NState> states)
    {
        Work();
        var set = new HashSet<NState>();
        var stack = new Stack<NState>();
        foreach (var state in states)
        {
            Scan();
            if (set.Add(state)) stack.Push(state);
        }
        while (stack.Count > 0)
        {
            Scan();
            var s = stack.Pop();
            foreach (var (label, to) in s.Edges)
            {
                Scan();
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
        private readonly BehaviorExplorer _owner;
        private readonly HashSet<string> _alphabet;
        private readonly List<HashSet<NState>> _sets = new();
        private readonly List<(bool Accept, bool Fail)> _flags = new();
        private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _moves = new(StringComparer.Ordinal);
        private readonly Dictionary<int, IReadOnlyDictionary<string, int>> _on = new();

        public SubsetDfa(BehaviorExplorer owner, NState nfaStart, HashSet<string> alphabet)
        {
            _owner = owner;
            _alphabet = alphabet;
            Start = GetOrAdd(owner.Closure(new[] { nfaStart }));
        }

        public int Start { get; }

        public void Check() => _owner.CheckBudget();

        private int GetOrAdd(HashSet<NState> s)
        {
            _owner.Work();
            var accept = false;
            var fail = false;
            var key = string.Join(",", s.Select(state =>
            {
                _owner.Scan();
                accept |= state.Accept;
                fail |= state.Fail;
                return state.Id;
            }).OrderBy(id => id));
            _owner.Work();
            if (_ids.TryGetValue(key, out var id)) return id;
            id = _sets.Count;
            _ids[key] = id;
            _sets.Add(s);
            _flags.Add((accept, fail));
            return id;
        }

        public bool Accept(int state)
        {
            Check();
            return _flags[state].Accept;
        }

        public bool Fail(int state)
        {
            Check();
            return _flags[state].Fail;
        }

        private int MoveTo(HashSet<NState> move)
        {
            _owner.Work();
            var key = string.Join(",", move.Select(node => { _owner.Scan(); return node.Id; }).OrderBy(id => id));
            _owner.CheckBudget();
            if (_moves.TryGetValue(key, out var cached)) return cached;
            var next = GetOrAdd(_owner.Closure(move));
            _moves[key] = next;
            return next;
        }

        public IReadOnlyDictionary<string, int> On(int state)
        {
            Check();
            if (_on.TryGetValue(state, out var cached)) return cached;
            using var walk = _owner.EnterWalk();

            var set = _sets[state];
            var d = new Dictionary<string, int>(StringComparer.Ordinal);

            var concrete = new Dictionary<string, HashSet<NState>>(StringComparer.Ordinal);
            var universal = new HashSet<NState>();
            var negated = new List<(string Excluded, NState To)>();
            foreach (var node in set)
            {
                _owner.Scan();
                foreach (var (label, to) in node.Edges)
                {
                    _owner.Scan();
                    if (label is null) continue;
                    if (label == "_") universal.Add(to);
                    else if (label.StartsWith("!", StringComparison.Ordinal)) negated.Add((label[1..], to));
                    else
                    {
                        if (!concrete.TryGetValue(label, out var targets))
                            concrete[label] = targets = new HashSet<NState>();
                        targets.Add(to);
                    }
                }
            }

            var symbols = new HashSet<string>(StringComparer.Ordinal);
            if (universal.Count > 0 || negated.Count > 0)
            {
                foreach (var symbol in _alphabet)
                {
                    _owner.Scan();
                    if (!symbol.Contains('(')) symbols.Add(symbol);
                }
            }

            foreach (var symbol in concrete.Keys)
            {
                _owner.Scan();
                symbols.Add(symbol);
            }

            foreach (var sym in symbols)
            {
                _owner.Scan();
                var move = new HashSet<NState>();
                if (concrete.TryGetValue(sym, out var targets))
                {
                    foreach (var target in targets)
                    {
                        _owner.Scan();
                        move.Add(target);
                    }
                }

                foreach (var target in universal)
                {
                    _owner.Scan();
                    move.Add(target);
                }

                foreach (var (excluded, target) in negated)
                {
                    _owner.Scan();
                    if (excluded != sym) move.Add(target);
                }

                if (move.Count > 0) d[sym] = MoveTo(move);
            }

            _on[state] = d;
            return d;
        }
    }

    /// <summary>Lazy product of two DFAs for the parallel operators (sync / interleave /
    /// sync-interleave). Product states (pairs of sub-DFA states) are created on demand.</summary>
    private sealed class ProductDfa : IDfa
    {
        private readonly BehaviorExplorer _owner;
        private readonly IDfa _l;
        private readonly IDfa _r;
        private readonly string _op;
        private readonly HashSet<string>? _syncInterleaveShared;
        private readonly List<(int L, int R)> _pairs = new();
        private readonly Dictionary<(int, int), int> _ids = new();
        private readonly Dictionary<int, IReadOnlyDictionary<string, int>> _on = new();

        public ProductDfa(BehaviorExplorer owner, IDfa l, IDfa r, string op)
        {
            _owner = owner;
            _l = l;
            _r = r;
            _op = op;
            // Sync-interleave (`|?|`) synchronizes on the shared *signature* — the full alphabets
            // of both sides — not per-state outgoing symbols, so compute it up front (the sides
            // are small; this is not used for `sync`/`interleave`).
            if (op == "syncinterleave")
            {
                var la = owner.FullAlphabet(l);
                var ra = owner.FullAlphabet(r);
                foreach (var symbol in la.ToArray())
                {
                    owner.Scan();
                    if (!ra.Contains(symbol)) la.Remove(symbol);
                }
                _syncInterleaveShared = la;
            }

            Start = GetOrAdd(l.Start, r.Start);
        }

        public int Start { get; }

        public void Check() => _owner.CheckBudget();

        private int GetOrAdd(int li, int ri)
        {
            _owner.Work();
            if (_ids.TryGetValue((li, ri), out var id)) return id;
            id = _pairs.Count;
            _ids[(li, ri)] = id;
            _pairs.Add((li, ri));
            return id;
        }

        public bool Accept(int state)
        {
            using var walk = _owner.EnterWalk(candidate: false);
            var (li, ri) = _pairs[state];
            return _l.Accept(li) && _r.Accept(ri);
        }

        public bool Fail(int state)
        {
            using var walk = _owner.EnterWalk(candidate: false);
            var (li, ri) = _pairs[state];
            return _l.Fail(li) || _r.Fail(ri);
        }

        public IReadOnlyDictionary<string, int> On(int state)
        {
            Check();
            if (_on.TryGetValue(state, out var cached)) return cached;
            using var walk = _owner.EnterWalk();

            var (li, ri) = _pairs[state];
            var lon = _l.On(li);
            var ron = _r.On(ri);
            var union = new HashSet<string>(StringComparer.Ordinal);
            foreach (var symbol in lon.Keys.Concat(ron.Keys))
            {
                _owner.Scan();
                union.Add(symbol);
            }

            var d = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var sym in union)
            {
                _owner.Scan();
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
    private HashSet<string> FullAlphabet(IDfa dfa)
    {
        var syms = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<int> { dfa.Start };
        var queue = new Queue<int>();
        queue.Enqueue(dfa.Start);
        while (queue.Count > 0)
        {
            Work();
            var s = queue.Dequeue();
            foreach (var (k, to) in dfa.On(s))
            {
                Scan();
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

    private ExplorationGraph ToGraph(string machine, IDfa dfa)
    {
        var graph = new ExplorationGraph { Machine = machine };
        var visited = new HashSet<int>();
        var queue = new Queue<(int State, int Depth)>();

        bool Admit(int state, int depth)
        {
            var accepting = dfa.Accept(state);
            if (!_budget.TryState()) return false;
            visited.Add(state);
            graph.States.Add(new ModelState("S" + state, "S" + state, Label: null, Accepting: accepting, Initial: state == dfa.Start));
            queue.Enqueue((state, depth));
            return true;
        }

        try
        {
            if (_options.MaxStates == 0) _budget.Stop("state-limit");
            else if (Admit(dfa.Start, 0)) graph.InitialStateId = "S" + dfa.Start;

            while (queue.Count > 0 && _budget.Check())
            {
                Work();
                var (state, depth) = queue.Dequeue();
                var outgoing = dfa.On(state).Select(edge => { Work(); return edge; })
                    .OrderBy(edge => edge.Key, StringComparer.Ordinal);
                foreach (var (symbol, next) in outgoing)
                {
                    Work();
                    if (graph.Transitions.Count >= _options.MaxTransitions)
                    {
                        _budget.Stop("transition-limit");
                        break;
                    }

                    if (!visited.Contains(next))
                    {
                        if (depth >= _options.MaxDepth)
                        {
                            _budget.Stop("depth-limit");
                            break;
                        }

                        if (graph.States.Count >= _options.MaxStates)
                        {
                            _budget.Stop("state-limit");
                            break;
                        }

                        if (!Admit(next, depth + 1)) break;
                    }

                    if (!_budget.TryTransition()) break;
                    graph.Transitions.Add(new Transition("S" + state, new ActionInvocation(symbol, Array.Empty<string>()), "S" + next));
                }
            }
        }
        catch (InvalidOperationException) when (_budget.TerminationReason is not null)
        {
        }

        _budget.Check();
        graph.Metadata["states"] = graph.States.Count.ToString();
        graph.Metadata["transitions"] = graph.Transitions.Count.ToString();
        graph.Metadata["accepting"] = graph.States.Count(s => s.Accepting).ToString();
        graph.Metadata["mode"] = "behavior";
        graph.Metadata["completeness"] = _budget.TerminationReason is null ? "COMPLETE" : "INCOMPLETE";
        graph.Metadata["terminationReason"] = _budget.TerminationReason ?? "complete";
        graph.Metadata["candidateEvaluations"] = _budget.CandidateEvaluations.ToString();
        graph.Metadata["elapsedMilliseconds"] = _budget.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return graph;
    }
}
