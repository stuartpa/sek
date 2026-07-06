using System;
using System.Collections.Generic;
using System.Linq;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Final reachable-branch coverage for <c>SpecExplorerKit.Components.Solving</c>: the Z3
/// untranslatable-predicate → C# post-filter fallback, and the predicate evaluator's
/// unknown-node / numeric-coercion fallbacks.
/// </summary>
public class SolvingFallbackTests
{
    private sealed class UnknownExpr : Expr { }

    private static Dictionary<string, object?> A(params (string, object?)[] kv)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }

    private static VarExpr Var(string n) => new() { Name = n };
    private static LitExpr Int(long v) => new() { Value = v, Kind = ValueKind.Int };
    private static BinExpr Bin(string op, Expr l, Expr r) => new() { Op = op, Left = l, Right = r };

    [Fact]
    public void PredicateEval_UnknownExprNode_YieldsNull()
    {
        Assert.Null(PredicateEval.Evaluate(new UnknownExpr(), A()));
        Assert.False(PredicateEval.Eval(new UnknownExpr(), A()));
    }

    [Fact]
    public void PredicateEval_NumericCoercion_BoolEnumString()
    {
        // bool operand coerces to 1; enum to underlying; numeric string parses.
        Assert.Equal(1L, PredicateEval.Evaluate(Bin("+", new LitExpr { Value = true, Kind = ValueKind.Bool }, Int(0)), A()));
        Assert.Equal(3L, PredicateEval.Evaluate(Bin("+", Var("d"), Int(0)), A(("d", DayOfWeek.Wednesday)))); // Wednesday = 3
        Assert.Equal(42L, PredicateEval.Evaluate(Bin("+", Var("s"), Int(0)), A(("s", "42"))));               // string parses
    }

    [Fact]
    public void Z3_UntranslatablePredicate_FallsBackToPostFilter()
    {
        // A top-level arithmetic op is not a boolean Z3 can add; TransBool throws
        // NotSupportedException, so it is enforced as a C# post-filter (PredicateEval), which
        // rejects the non-boolean result → empty. This exercises the catch/post-filter path.
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int, Domain = new object?[] { 1L, 2L } } };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = Bin("+", Var("a"), Int(1)) } };
        var res = new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);
        Assert.Empty(res);
    }
}
