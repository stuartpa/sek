using System;
using System.Collections.Generic;
using System.Linq;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Edge-branch coverage for <c>SpecExplorerKit.Components.Solving</c>'s Z3 backend: the zero-
/// parameter case, an unconstrained (predicate-only) integer parameter that exercises the
/// value-map fallback, and untranslatable nodes routed to the C# post-filter.
/// </summary>
public class SolvingEdgeTests
{
    private sealed class UnknownExpr : Expr { }

    private static VarExpr Var(string n) => new() { Name = n };
    private static LitExpr Int(long v) => new() { Value = v, Kind = ValueKind.Int };
    private static BinExpr Bin(string op, Expr l, Expr r) => new() { Op = op, Left = l, Right = r };

    [Fact]
    public void Z3_NoParameters_YieldsSingleEmptyCombination()
    {
        var res = new Z3Solver().Generate(new List<SolverParam>(), new List<SolverConstraint>(), new CombinationSpec(), 100);
        Assert.Single(res);
        Assert.Empty(res[0]);
    }

    [Fact]
    public void Z3_PredicateOnlyIntParam_NoDomain_MapsRawValue()
    {
        // No In/Domain: the int const is bounded only by the predicate 0 < a < 3 → {1,2}.
        // MapBack has no candidate list to match, so it returns the raw solved value.
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int } };
        var cons = new List<SolverConstraint>
        {
            new PredicateConstraint { Expr = Bin("&&", Bin(">", Var("a"), Int(0)), Bin("<", Var("a"), Int(3))) },
        };
        var res = new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);
        var vals = res.Select(r => Convert.ToInt64(r["a"])).OrderBy(x => x).ToList();
        Assert.Equal(new long[] { 1, 2 }, vals);
    }

    [Fact]
    public void Z3_UnknownNode_InBoolContext_FallsBackToPostFilter()
    {
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int, Domain = new object?[] { 1L, 2L } } };
        // Top-level unknown expr → TransBool default throw → caught → post-filter (rejects non-bool).
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = new UnknownExpr() } };
        Assert.Empty(new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100));
    }

    [Fact]
    public void Z3_UnknownNode_InIntContext_FallsBackToPostFilter()
    {
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int, Domain = new object?[] { 1L, 2L } } };
        // Unknown expr inside a comparison → TransInt default throw → caught → post-filter. The C#
        // evaluator coerces the unknown node to 0, so 0 < 1 holds and both candidates survive —
        // the point is that the fallback path executes rather than the constraint being enforced in Z3.
        var cons = new List<SolverConstraint>
        {
            new PredicateConstraint { Expr = Bin("<", new UnknownExpr(), Int(1)) },
        };
        Assert.Equal(2, new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100).Count);
    }
}
