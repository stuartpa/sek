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
    public static ActionConstraints Extract(DeclaredAction action)
    {
        var result = new ActionConstraints();
        var paramNames = action.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(action.WhereCode))
        {
            return result;
        }

        foreach (var stmt in SplitStatements(action.WhereCode))
        {
            var s = stmt.Trim();
            if (s.Length == 0)
            {
                continue;
            }

            if (s.StartsWith("Condition.In", StringComparison.Ordinal))
            {
                var inC = ParseIn(s);
                if (inC is not null && paramNames.Contains(inC.Param))
                {
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

        return result;
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
