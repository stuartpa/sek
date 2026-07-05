using System.Text;
using Sek.Cord.Ast;
using Sek.Solver;

namespace Sek.Cord;

/// <summary>
/// Turns a Cord declared-action <c>where {. ... .}</c> block into solver constraints:
/// <c>Condition.In(param, v...)</c> -> <see cref="InConstraint"/>,
/// <c>Condition.IsTrue(expr)</c> -> <see cref="PredicateConstraint"/> (kept only if it
/// references known parameters), and <c>Combination.Pairwise(...)</c> -> pairwise mode.
/// </summary>
public sealed class ActionConstraints
{
    public List<SolverConstraint> Constraints { get; } = new();
    public CombinationSpec Combination { get; } = new();
}

public static class CordConstraintExtractor
{
    public static ActionConstraints Extract(DeclaredAction action, int randomSeed = 0)
    {
        var result = new ActionConstraints();
        var paramNames = action.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(action.WhereCode))
        {
            return result;
        }

        // A seeded gate for `Probability.IsTrue(p)` branch selection: reproducible for a given
        // `switch RandomSeed`, and consulted sequentially as the where-block is scanned.
        var probability = new Sek.Solver.ProbabilityGate(randomSeed);

        // Strip C# comments so a `// ...` line preceding a statement does not swallow it when
        // the block is split on `;` (a chunk beginning with a comment would fail the
        // `StartsWith("Condition...")` dispatch and be dropped).
        var whereCode = StripComments(action.WhereCode);

        // First pass: collect where-block local declarations `Type name = expr;` (e.g.
        // `uint mon = days & 0x1;`) so Combination columns can reference them.
        var locals = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var stmt in SplitStatements(whereCode))
        {
            var s = stmt.Trim();
            if (TryParseLocal(s, paramNames, out var localName, out var localExpr))
            {
                locals[localName] = localExpr;
            }
        }

        var probThenFirst = true;            // seeded order of a probabilistic parameter's values
        var probParams = new HashSet<string>(StringComparer.Ordinal);
        string? curBranch = null;            // "then" / "else" while inside a probabilistic branch
        foreach (var stmt in SplitStatements(whereCode))
        {
            var s = stmt.Trim();
            if (s.Length == 0)
            {
                continue;
            }

            curBranch = null;

            // Probabilistic branch: `if (Probability.IsTrue(p)) <then>` / `else <else>`. Both
            // branches' values are reachable, so the parameter's domain is their union; the seeded
            // gate only chooses the reproducible order (the more-likely branch's values first),
            // which matters when a bounded generation samples a prefix of the domain.
            if (s.StartsWith("if ", StringComparison.Ordinal) || s.StartsWith("if(", StringComparison.Ordinal))
            {
                var open = s.IndexOf('(');
                var close = MatchParen(s, open);
                var cond = open >= 0 && close > open ? s[(open + 1)..close] : string.Empty;
                var thenStmt = close >= 0 && close + 1 <= s.Length ? s[(close + 1)..].Trim() : string.Empty;
                probThenFirst = EvalProbability(cond, probability);
                curBranch = "then";
                s = thenStmt;
            }
            else if (s.StartsWith("else", StringComparison.Ordinal))
            {
                curBranch = "else";
                s = s[4..].Trim();
            }

            if (s.StartsWith("Condition.In", StringComparison.Ordinal))
            {
                var inC = ParseIn(s);
                // Keep a domain for a parameter, or for a struct field of a parameter
                // (e.g. Condition.In(info.Command, ...) where `info` is a struct parameter).
                if (inC is not null && (paramNames.Contains(inC.Param) || IsFieldOfParam(inC.Param, paramNames)))
                {
                    if (curBranch is not null) probParams.Add(inC.Param);
                    result.Constraints.Add(inC);
                }
            }
            else if (s.StartsWith("Condition.IsTrue", StringComparison.Ordinal))
            {
                var inner = ArgsInside(s);
                var (expr, refs, ok) = ExprParser.TryParse(inner);
                // Keep the predicate if every simple identifier is a parameter. Dotted
                // identifiers (e.g. Frequency.Daily) are enum literals that the engine
                // resolves later against the rule's parameter types.
                if (ok && expr is not null && refs.All(r => r.Contains('.') || paramNames.Contains(r)))
                {
                    result.Constraints.Add(new PredicateConstraint { Expr = expr });
                }
                else if (MentionsParam(inner, paramNames))
                {
                    // The expression is outside the mini-parser's grammar (method calls,
                    // Math.*, string members, %, ternary, ...). Execute it as embedded C#
                    // via Roslyn; if it compiles, keep it as a compiled post-filter.
                    var pred = RoslynPredicate.TryCompile(inner, SolverParamsOf(action));
                    if (pred is not null)
                    {
                        result.Constraints.Add(new CompiledPredicateConstraint { Source = inner, Predicate = pred });
                    }
                }
            }
            else if (s.StartsWith("Combination.Pairwise", StringComparison.Ordinal))
            {
                result.Combination.Mode = CombinationSpec.Strategy.Pairwise;
                // If any Pairwise argument is a derived expression (a where-local or a compound
                // expression like `days & DaysOfWeek.Mon`), treat every argument as a column.
                var args = SplitArgs(ArgsInside(s)).Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
                var anyDerived = args.Any(a => locals.ContainsKey(a) || !(paramNames.Contains(a) || IsFieldOfParam(a, paramNames)));
                if (anyDerived)
                {
                    foreach (var a in args)
                    {
                        var exprText = locals.TryGetValue(a, out var le) ? le : a;
                        var (cexpr, _, cok) = ExprParser.TryParse(exprText);
                        if (cok && cexpr is not null)
                        {
                            result.Combination.PairwiseColumns.Add((a, cexpr));
                        }
                    }
                }
            }
            else if (s.StartsWith("Combination.Expand", StringComparison.Ordinal))
            {
                foreach (var a in SplitArgs(ArgsInside(s)))
                {
                    var name = a.Trim();
                    if (paramNames.Contains(name))
                    {
                        result.Combination.Expand.Add(name);
                    }
                }
            }
            else if (s.StartsWith("Combination.Isolated", StringComparison.Ordinal))
            {
                var (expr, refs, ok) = ExprParser.TryParse(ArgsInside(s));
                if (ok && expr is not null && refs.All(r => r.Contains('.') || paramNames.Contains(r)))
                {
                    result.Combination.Isolated.Add(expr);
                }
            }
            else if (s.StartsWith("Combination.Seeded", StringComparison.Ordinal))
            {
                var conj = new List<Sek.Solver.Expr>();
                var okAll = true;
                foreach (var a in SplitArgs(ArgsInside(s)))
                {
                    var (e, refs, ok) = ExprParser.TryParse(a);
                    if (ok && e is not null && refs.All(r => r.Contains('.') || paramNames.Contains(r)))
                    {
                        conj.Add(e);
                    }
                    else
                    {
                        okAll = false;
                        break;
                    }
                }

                if (okAll && conj.Count > 0)
                {
                    result.Combination.Seeded.Add(conj);
                }
            }
            // Combination.Interaction => default (full cartesian product).
        }

        // Merge duplicate `Condition.In` domains for the same parameter into one (the solvers key
        // domains by parameter). A parameter split across a probabilistic if/else gets the union
        // of both branches, ordered so the seeded-preferred branch's values come first.
        MergeInConstraints(result, probParams, probThenFirst);

        return result;
    }

    /// <summary>Collapses multiple <see cref="InConstraint"/>s for the same parameter into a single
    /// one whose values are the (distinct) union. For a probabilistic parameter the values are
    /// ordered by <paramref name="probThenFirst"/> — the gate-preferred branch first.</summary>
    private static void MergeInConstraints(ActionConstraints result, HashSet<string> probParams, bool probThenFirst)
    {
        var ins = result.Constraints.OfType<InConstraint>().ToList();
        var byParam = ins.GroupBy(c => c.Param, StringComparer.Ordinal).Where(g => g.Count() > 1).ToList();
        if (byParam.Count == 0) return;

        foreach (var group in byParam)
        {
            var ordered = group.AsEnumerable();
            // For a probabilistic param, `else` values were added after `then`; reverse the branch
            // order when the gate prefers the `else` branch.
            if (!probThenFirst && probParams.Contains(group.Key)) ordered = group.Reverse();

            var merged = new InConstraint { Param = group.Key };
            foreach (var c in ordered)
            {
                foreach (var v in c.Values)
                {
                    if (!merged.Values.Any(x => Equals(x, v))) merged.Values.Add(v);
                }
            }

            var firstIndex = result.Constraints.IndexOf(group.First());
            foreach (var c in group) result.Constraints.Remove(c);
            result.Constraints.Insert(Math.Min(firstIndex, result.Constraints.Count), merged);
        }
    }

    /// <summary>True if <paramref name="token"/> is a struct field access <c>param.field</c>
    /// whose leading segment is a known parameter.</summary>
    private static bool IsFieldOfParam(string token, HashSet<string> paramNames)
    {
        var dot = token.IndexOf('.');
        return dot > 0 && paramNames.Contains(token[..dot]);
    }

    /// <summary>Index of the parenthesis matching the one at <paramref name="open"/>, or -1.</summary>
    private static int MatchParen(string s, int open)
    {
        if (open < 0 || open >= s.Length || s[open] != '(') return -1;
        var depth = 0;
        for (var i = open; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')' && --depth == 0) return i;
        }

        return -1;
    }

    /// <summary>Evaluates a <c>Probability.IsTrue(p)</c> condition with a seeded gate, so the
    /// chosen branch is reproducible for a given <c>RandomSeed</c> and varies across seeds.</summary>
    private static bool EvalProbability(string cond, Sek.Solver.ProbabilityGate gate)
    {
        var inner = ArgsInside(cond);
        return double.TryParse(inner.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p)
            ? gate.IsTrue(p)
            : true;
    }

    /// <summary>Recognizes a where-block local declaration <c>Type name = expr</c> (e.g.    /// <c>uint mon = days &amp; 0x1</c>) whose right-hand side references a parameter.</summary>
    private static bool TryParseLocal(string stmt, HashSet<string> paramNames, out string name, out string expr)
    {
        name = string.Empty;
        expr = string.Empty;
        if (stmt.StartsWith("Condition", StringComparison.Ordinal) || stmt.StartsWith("Combination", StringComparison.Ordinal))
        {
            return false;
        }

        var eq = stmt.IndexOf('=');
        if (eq <= 0) return false;
        var lhs = stmt[..eq].Trim();
        var rhs = stmt[(eq + 1)..].Trim();
        // lhs must be "Type name" (two identifiers) and rhs must mention a parameter.
        var parts = lhs.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        if (!MentionsParam(rhs, paramNames)) return false;
        name = parts[1];
        expr = rhs;
        return true;
    }

    /// <summary>True if any whole-word identifier in the code names a known parameter.</summary>
    private static bool MentionsParam(string code, HashSet<string> paramNames)
    {
        foreach (var name in paramNames)
        {
            var idx = code.IndexOf(name, StringComparison.Ordinal);
            while (idx >= 0)
            {
                var before = idx == 0 || (!char.IsLetterOrDigit(code[idx - 1]) && code[idx - 1] != '_' && code[idx - 1] != '.');
                var afterPos = idx + name.Length;
                var after = afterPos >= code.Length || (!char.IsLetterOrDigit(code[afterPos]) && code[afterPos] != '_');
                if (before && after)
                {
                    return true;
                }

                idx = code.IndexOf(name, idx + 1, StringComparison.Ordinal);
            }
        }

        return false;
    }

    /// <summary>Builds solver parameters (name + value kind) from a declared action.</summary>
    private static List<SolverParam> SolverParamsOf(DeclaredAction action) =>
        action.Parameters.Select(p => new SolverParam { Name = p.Name, Kind = KindOf(p.Type) }).ToList();

    private static ValueKind KindOf(string type)
    {
        var t = (type ?? string.Empty).Trim().ToLowerInvariant();
        return t switch
        {
            "string" => ValueKind.String,
            "bool" => ValueKind.Bool,
            "long" or "ulong" => ValueKind.Long,
            _ => ValueKind.Int,
        };
    }

    private static InConstraint? ParseIn(string stmt)
    {
        var inside = ArgsInside(stmt);
        var args = SplitArgs(inside);
        if (args.Count < 2)
        {
            return null;
        }

        var c = new InConstraint { Param = args[0].Trim() };
        for (var i = 1; i < args.Count; i++)
        {
            c.Values.Add(ParseLiteral(args[i].Trim()));
        }

        return c.Values.Count > 0 ? c : null;
    }

    private static object? ParseLiteral(string token)
    {
        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
        {
            return token[1..^1];
        }

        if (token == "true")
        {
            return true;
        }

        if (token == "false")
        {
            return false;
        }

        if (long.TryParse(token, out var l))
        {
            return l >= int.MinValue && l <= int.MaxValue ? (int)l : l;
        }

        return token; // fallback (e.g. enum member name)
    }

    /// <summary>Text between the first '(' and its matching ')'.</summary>
    private static string ArgsInside(string s)
    {
        var open = s.IndexOf('(');
        if (open < 0)
        {
            return string.Empty;
        }

        var depth = 0;
        var sb = new StringBuilder();
        for (var i = open; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == '(') { depth++; if (depth == 1) continue; }
            else if (ch == ')') { depth--; if (depth == 0) break; }
            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static List<string> SplitArgs(string s) => SplitTopLevel(s, ',');

    private static List<string> SplitStatements(string s) => SplitTopLevel(s, ';');

    /// <summary>Removes C# line (<c>// ...</c>) and block (<c>/* ... */</c>) comments,
    /// preserving string literals, so comments in a <c>where {. ... .}</c> block do not
    /// interfere with statement splitting.</summary>
    private static string StripComments(string s)
    {
        var sb = new StringBuilder(s.Length);
        var inStr = false;
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (inStr)
            {
                sb.Append(ch);
                if (ch == '"') inStr = false;
                continue;
            }

            if (ch == '"') { inStr = true; sb.Append(ch); continue; }

            if (ch == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n') i++;
                if (i < s.Length) sb.Append('\n');
                continue;
            }

            if (ch == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) i++;
                i++; // skip the closing '/'
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static List<string> SplitTopLevel(string s, char sep)
    {
        var parts = new List<string>();
        var depth = 0;
        var inStr = false;
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == '"')
            {
                inStr = !inStr;
                sb.Append(ch);
            }
            else if (!inStr && (ch == '(' || ch == '<'))
            {
                depth++;
                sb.Append(ch);
            }
            else if (!inStr && (ch == ')' || ch == '>'))
            {
                depth--;
                sb.Append(ch);
            }
            else if (!inStr && ch == sep && depth == 0)
            {
                parts.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        if (sb.Length > 0)
        {
            parts.Add(sb.ToString());
        }

        return parts;
    }
}
