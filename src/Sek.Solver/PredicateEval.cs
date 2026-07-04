namespace Sek.Solver;

/// <summary>Evaluates a predicate <see cref="Expr"/> against a concrete assignment (C# side).</summary>
public static class PredicateEval
{
    public static bool Eval(Expr expr, IReadOnlyDictionary<string, object?> assignment)
    {
        var v = Value(expr, assignment);
        return v is bool b && b;
    }

    private static object? Value(Expr expr, IReadOnlyDictionary<string, object?> a)
    {
        switch (expr)
        {
            case LitExpr lit:
                return lit.Value;
            case VarExpr var:
                return a.TryGetValue(var.Name, out var val) ? val : null;
            case UnExpr un:
                var o = Value(un.Operand, a);
                return un.Op switch
                {
                    "!" => !ToBool(o),
                    "-" => -ToLong(o),
                    _ => null,
                };
            case BinExpr bin:
                return EvalBin(bin, a);
            default:
                return null;
        }
    }

    private static object? EvalBin(BinExpr bin, IReadOnlyDictionary<string, object?> a)
    {
        var l = Value(bin.Left, a);
        var r = Value(bin.Right, a);
        switch (bin.Op)
        {
            case "&&":
            case "&" when l is bool || r is bool:
                return ToBool(l) && ToBool(r);
            case "||":
            case "|" when l is bool || r is bool:
                return ToBool(l) || ToBool(r);
            case "==":
                return AreEqual(l, r);
            case "!=":
                return !AreEqual(l, r);
            case "<":
                return ToLong(l) < ToLong(r);
            case "<=":
                return ToLong(l) <= ToLong(r);
            case ">":
                return ToLong(l) > ToLong(r);
            case ">=":
                return ToLong(l) >= ToLong(r);
            case "+":
                return ToLong(l) + ToLong(r);
            case "-":
                return ToLong(l) - ToLong(r);
            case "*":
                return ToLong(l) * ToLong(r);
            case "/":
                return ToLong(r) != 0 ? ToLong(l) / ToLong(r) : 0;
            case "%":
                return ToLong(r) != 0 ? ToLong(l) % ToLong(r) : 0;
            case "&":
                return ToLong(l) & ToLong(r);
            case "|":
                return ToLong(l) | ToLong(r);
            default:
                return null;
        }
    }

    private static bool AreEqual(object? l, object? r)
    {
        if (l is null || r is null)
        {
            return Equals(l, r);
        }

        if (l is string || r is string)
        {
            return string.Equals(l.ToString(), r.ToString(), StringComparison.Ordinal);
        }

        if (l is bool || r is bool)
        {
            return ToBool(l) == ToBool(r);
        }

        return ToLong(l) == ToLong(r);
    }

    private static bool ToBool(object? o) => o is bool b ? b : Convert.ToInt64(o ?? 0L) != 0;

    private static long ToLong(object? o) => o switch
    {
        null => 0L,
        bool b => b ? 1L : 0L,
        Enum e => Convert.ToInt64(e),
        _ => long.TryParse(o.ToString(), out var l) ? l : Convert.ToInt64(o),
    };
}
