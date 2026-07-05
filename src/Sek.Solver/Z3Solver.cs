using Microsoft.Z3;

namespace Sek.Solver;

/// <summary>
/// Z3-backed parameter generator. Each parameter becomes an SMT constant (integers for
/// numeric/enum values, booleans, or an index for finite string domains). Domain
/// membership (<c>Condition.In</c>) and translatable predicates (<c>Condition.IsTrue</c>)
/// are added as constraints; Z3 then enumerates satisfying assignments (blocking each
/// found model). Predicates Z3 can't represent are applied as a C# post-filter.
/// </summary>
public sealed class Z3Solver : IParameterSolver
{
    public string Name => "z3";

    private const int EnumerationCap = 200000;

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Generate(
        IReadOnlyList<SolverParam> parameters,
        IReadOnlyList<SolverConstraint> constraints,
        CombinationSpec combination,
        int limit)
    {
        var inByParam = constraints.OfType<InConstraint>().ToDictionary(c => c.Param, c => c.Values);
        var predicates = constraints.OfType<PredicateConstraint>().Select(p => p.Expr).ToList();
        var compiled = constraints.OfType<CompiledPredicateConstraint>().ToList();

        // Effective candidate values per parameter.
        var candidates = new Dictionary<string, List<object?>>();
        foreach (var p in parameters)
        {
            var vals = inByParam.TryGetValue(p.Name, out var inVals)
                ? inVals
                : (p.Domain?.ToList() ?? new List<object?>());
            candidates[p.Name] = vals.Distinct().ToList();
        }

        var results = new List<IReadOnlyDictionary<string, object?>>();
        var postFilter = new List<Expr>();

        using (var ctx = new Context())
        {
            var solver = ctx.MkSolver();
            var intConsts = new Dictionary<string, IntExpr>();
            var boolConsts = new Dictionary<string, BoolExpr>();
            var stringIndex = new Dictionary<string, List<string>>();

            foreach (var p in parameters)
            {
                var cand = candidates[p.Name];
                switch (p.Kind)
                {
                    case ValueKind.Bool:
                    {
                        var c = (BoolExpr)ctx.MkBoolConst(p.Name);
                        boolConsts[p.Name] = c;
                        if (cand.Count > 0)
                        {
                            solver.Add(ctx.MkOr(cand.Select(v => ctx.MkEq(c, ctx.MkBool(Convert.ToBoolean(v)))).ToArray()));
                        }

                        break;
                    }

                    case ValueKind.String:
                    {
                        var list = cand.Select(v => v?.ToString() ?? string.Empty).ToList();
                        stringIndex[p.Name] = list;
                        var c = ctx.MkIntConst(p.Name);
                        intConsts[p.Name] = c;
                        if (list.Count > 0)
                        {
                            solver.Add(ctx.MkOr(Enumerable.Range(0, list.Count).Select(i => ctx.MkEq(c, ctx.MkInt(i))).ToArray()));
                        }

                        break;
                    }

                    default: // Int / Long / enum-as-int
                    {
                        var c = ctx.MkIntConst(p.Name);
                        intConsts[p.Name] = c;
                        if (cand.Count > 0)
                        {
                            solver.Add(ctx.MkOr(cand.Select(v => ctx.MkEq(c, ctx.MkInt(ToLong(v)))).ToArray()));
                        }

                        break;
                    }
                }
            }

            foreach (var pred in predicates)
            {
                try
                {
                    solver.Add(TransBool(ctx, pred, parameters, intConsts, boolConsts, stringIndex, candidates));
                }
                catch (NotSupportedException)
                {
                    postFilter.Add(pred); // enforce later in C#
                }
            }

            while (results.Count < EnumerationCap && solver.Check() == Status.SATISFIABLE)
            {
                var model = solver.Model;
                var assignment = new Dictionary<string, object?>();
                var blocking = new List<BoolExpr>();

                foreach (var p in parameters)
                {
                    if (p.Kind == ValueKind.Bool)
                    {
                        var c = boolConsts[p.Name];
                        var val = model.Evaluate(c, true).IsTrue;
                        assignment[p.Name] = val;
                        blocking.Add(ctx.MkNot(ctx.MkEq(c, ctx.MkBool(val))));
                    }
                    else
                    {
                        var c = intConsts[p.Name];
                        var n = ((IntNum)model.Evaluate(c, true)).Int64;
                        blocking.Add(ctx.MkNot(ctx.MkEq(c, ctx.MkInt(n))));
                        assignment[p.Name] = MapBack(p, candidates[p.Name], stringIndex, n);
                    }
                }

                results.Add(assignment);
                if (blocking.Count > 0)
                {
                    solver.Add(ctx.MkOr(blocking.ToArray()));
                }
                else
                {
                    break;
                }
            }
        }

        var filtered = postFilter.Count == 0
            ? results
            : results.Where(a => postFilter.All(e => PredicateEval.Eval(e, a))).ToList();

        if (compiled.Count > 0)
        {
            filtered = filtered.Where(a => compiled.All(c => c.Predicate(a))).ToList();
        }

        return Combinatorics.Apply(parameters.Select(p => p.Name).ToList(), filtered, combination, limit);
    }

    private static object? MapBack(SolverParam p, List<object?> candidates, Dictionary<string, List<string>> stringIndex, long n)
    {
        if (p.Kind == ValueKind.String && stringIndex.TryGetValue(p.Name, out var list))
        {
            return n >= 0 && n < list.Count ? list[(int)n] : null;
        }

        // Return the original candidate object whose numeric value matches (keeps enums/ints).
        foreach (var v in candidates)
        {
            if (ToLong(v) == n)
            {
                return v;
            }
        }

        return n;
    }

    // ---- Predicate translation to Z3 ------------------------------------------

    private static BoolExpr TransBool(
        Context ctx, Expr e, IReadOnlyList<SolverParam> ps,
        Dictionary<string, IntExpr> ints, Dictionary<string, BoolExpr> bools,
        Dictionary<string, List<string>> strIdx, Dictionary<string, List<object?>> cands)
    {
        switch (e)
        {
            case UnExpr un when un.Op == "!":
                return ctx.MkNot(TransBool(ctx, un.Operand, ps, ints, bools, strIdx, cands));
            case VarExpr v when bools.ContainsKey(v.Name):
                return bools[v.Name];
            case LitExpr lit when lit.Kind == ValueKind.Bool:
                return ctx.MkBool(Convert.ToBoolean(lit.Value));
            case BinExpr bin:
                return TransBoolBin(ctx, bin, ps, ints, bools, strIdx, cands);
            default:
                throw new NotSupportedException();
        }
    }

    private static BoolExpr TransBoolBin(
        Context ctx, BinExpr bin, IReadOnlyList<SolverParam> ps,
        Dictionary<string, IntExpr> ints, Dictionary<string, BoolExpr> bools,
        Dictionary<string, List<string>> strIdx, Dictionary<string, List<object?>> cands)
    {
        switch (bin.Op)
        {
            case "&&":
            case "&" when IsBoolCtx(bin, bools):
                return ctx.MkAnd(TransBool(ctx, bin.Left, ps, ints, bools, strIdx, cands), TransBool(ctx, bin.Right, ps, ints, bools, strIdx, cands));
            case "||":
            case "|" when IsBoolCtx(bin, bools):
                return ctx.MkOr(TransBool(ctx, bin.Left, ps, ints, bools, strIdx, cands), TransBool(ctx, bin.Right, ps, ints, bools, strIdx, cands));
            case "==":
            case "!=":
                if (IsString(bin.Left, strIdx) || IsString(bin.Right, strIdx))
                {
                    var eq = ctx.MkEq(StrIndex(ctx, bin.Left, bin.Right, ints, strIdx), StrIndex(ctx, bin.Right, bin.Left, ints, strIdx));
                    return bin.Op == "==" ? eq : ctx.MkNot(eq);
                }

                var l = TransInt(ctx, bin.Left, ints, cands);
                var r = TransInt(ctx, bin.Right, ints, cands);
                return bin.Op == "==" ? ctx.MkEq(l, r) : ctx.MkNot(ctx.MkEq(l, r));
            case "<":
                return ctx.MkLt(TransInt(ctx, bin.Left, ints, cands), TransInt(ctx, bin.Right, ints, cands));
            case "<=":
                return ctx.MkLe(TransInt(ctx, bin.Left, ints, cands), TransInt(ctx, bin.Right, ints, cands));
            case ">":
                return ctx.MkGt(TransInt(ctx, bin.Left, ints, cands), TransInt(ctx, bin.Right, ints, cands));
            case ">=":
                return ctx.MkGe(TransInt(ctx, bin.Left, ints, cands), TransInt(ctx, bin.Right, ints, cands));
            default:
                throw new NotSupportedException();
        }
    }

    private static bool IsBoolCtx(BinExpr bin, Dictionary<string, BoolExpr> bools) =>
        (bin.Left is VarExpr lv && bools.ContainsKey(lv.Name))
        || (bin.Right is VarExpr rv && bools.ContainsKey(rv.Name))
        || bin.Left is BinExpr || bin.Right is BinExpr
        || (bin.Left is UnExpr) || (bin.Right is UnExpr)
        || (bin.Left is LitExpr ll && ll.Kind == ValueKind.Bool);

    private static bool IsString(Expr e, Dictionary<string, List<string>> strIdx) =>
        (e is VarExpr v && strIdx.ContainsKey(v.Name)) || (e is LitExpr l && l.Kind == ValueKind.String);

    private static IntExpr StrIndex(Context ctx, Expr e, Expr other, Dictionary<string, IntExpr> ints, Dictionary<string, List<string>> strIdx)
    {
        if (e is VarExpr v && ints.ContainsKey(v.Name))
        {
            return ints[v.Name];
        }

        if (e is LitExpr lit && lit.Kind == ValueKind.String && other is VarExpr ov && strIdx.TryGetValue(ov.Name, out var list))
        {
            var i = list.IndexOf(lit.Value?.ToString() ?? string.Empty);
            return (IntExpr)ctx.MkInt(i); // -1 if absent => never matches, which is correct
        }

        throw new NotSupportedException();
    }

    private static ArithExpr TransInt(Context ctx, Expr e, Dictionary<string, IntExpr> ints, Dictionary<string, List<object?>> cands)
    {
        switch (e)
        {
            case VarExpr v when ints.ContainsKey(v.Name):
                return ints[v.Name];
            case LitExpr lit when lit.Kind is ValueKind.Int or ValueKind.Long:
                return (ArithExpr)ctx.MkInt(ToLong(lit.Value));
            case UnExpr un when un.Op == "-":
                return ctx.MkUnaryMinus(TransInt(ctx, un.Operand, ints, cands));
            case BinExpr bin:
                var l = TransInt(ctx, bin.Left, ints, cands);
                var r = TransInt(ctx, bin.Right, ints, cands);
                return bin.Op switch
                {
                    "+" => ctx.MkAdd(l, r),
                    "-" => ctx.MkSub(l, r),
                    "*" => ctx.MkMul(l, r),
                    "/" => ctx.MkDiv(l, r),
                    "%" => ctx.MkMod((IntExpr)l, (IntExpr)r),
                    "&" => (IntExpr)ctx.MkBV2Int(ctx.MkBVAND(ctx.MkInt2BV(32, (IntExpr)l), ctx.MkInt2BV(32, (IntExpr)r)), false),
                    "|" => (IntExpr)ctx.MkBV2Int(ctx.MkBVOR(ctx.MkInt2BV(32, (IntExpr)l), ctx.MkInt2BV(32, (IntExpr)r)), false),
                    _ => throw new NotSupportedException(),
                };
            default:
                throw new NotSupportedException();
        }
    }

    private static long ToLong(object? o) => o switch
    {
        null => 0L,
        bool b => b ? 1L : 0L,
        Enum en => Convert.ToInt64(en),
        _ => long.TryParse(o.ToString(), out var l) ? l : Convert.ToInt64(o),
    };
}
