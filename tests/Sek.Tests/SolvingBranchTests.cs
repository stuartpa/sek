using System;
using System.Collections.Generic;
using System.Linq;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch-closing tests for <c>SpecExplorerKit.Components.Solving</c>: the reachable edge branches
/// of the combinatorial reducer, the predicate evaluator's fallbacks, and the Z3 string
/// inequality path. (Defensive `throw NotSupportedException` branches and the 200k enumeration cap
/// are documented as deliberately-uncovered in the coverage report.)
/// </summary>
public class SolvingBranchTests
{
    private static Dictionary<string, object?> A(params (string, object?)[] kv)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }

    private static VarExpr Var(string n) => new() { Name = n };
    private static LitExpr Int(long v) => new() { Value = v, Kind = ValueKind.Int };
    private static LitExpr Str(string v) => new() { Value = v, Kind = ValueKind.String };
    private static BinExpr Bin(string op, Expr l, Expr r) => new() { Op = op, Left = l, Right = r };
    private static UnExpr Un(string op, Expr o) => new() { Op = op, Operand = o };

    // ---- Combinatorics edge branches ---------------------------------------------------

    [Fact]
    public void Combinatorics_Pairwise_SingleParam_ReturnsAll()
    {
        var all = new List<IReadOnlyDictionary<string, object?>> { A(("a", 1)), A(("a", 2)) };
        var res = Combinatorics.Pairwise(new List<string> { "a" }, all); // count<=1 → early return
        Assert.Equal(2, res.Count);
    }

    [Fact]
    public void Combinatorics_Pairwise_Empty_ReturnsEmpty()
    {
        var res = Combinatorics.Pairwise(new List<string> { "a", "b" }, new List<IReadOnlyDictionary<string, object?>>());
        Assert.Empty(res);
    }

    [Fact]
    public void Combinatorics_Apply_Seeded_AddsRepresentativeMissingFromReducedWork()
    {
        // Pairwise reduction may drop the exact tuple a=1,b=1; a seed for it must re-add it.
        var names = new List<string> { "a", "b" };
        var all = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var a in new[] { 0, 1 })
        foreach (var b in new[] { 0, 1 })
            all.Add(A(("a", a), ("b", b)));
        var spec = new CombinationSpec { Mode = CombinationSpec.Strategy.Pairwise };
        spec.Seeded.Add(new List<Expr> { Bin("==", Var("a"), Int(1)), Bin("==", Var("b"), Int(1)) });
        var res = Combinatorics.Apply(names, all, spec, 100);
        Assert.Contains(res, r => Convert.ToInt64(r["a"]) == 1 && Convert.ToInt64(r["b"]) == 1);
    }

    // ---- PredicateEval fallbacks -------------------------------------------------------

    [Fact]
    public void PredicateEval_UnknownOperators_YieldNull()
    {
        Assert.Null(PredicateEval.Evaluate(Bin("^", Int(1), Int(2)), A()));       // unknown binary op
        Assert.Null(PredicateEval.Evaluate(Un("~", Int(1)), A()));                // unknown unary op
    }

    [Fact]
    public void PredicateEval_Equality_WithNulls()
    {
        Assert.True(PredicateEval.Eval(Bin("==", Var("x"), new LitExpr { Value = null }), A(("x", null))));
        Assert.False(PredicateEval.Eval(Bin("==", Var("x"), Int(1)), A(("x", null))));
    }

    [Fact]
    public void PredicateEval_ToLong_HandlesBoolEnumAndUnparseable()
    {
        // bool → 1/0 via arithmetic; enum → underlying; non-numeric string → Convert path
        Assert.Equal(1L, PredicateEval.Evaluate(Bin("+", new LitExpr { Value = true, Kind = ValueKind.Bool }, Int(0)), A()));
        Assert.Equal(2L, PredicateEval.Evaluate(Bin("+", Var("c"), Int(0)), A(("c", DayOfWeek.Tuesday)))); // enum=2
    }

    // ---- Z3 string inequality ----------------------------------------------------------

    [Fact]
    public void Z3_StringInequality_Predicate()
    {
        var ps = new List<SolverParam> { new() { Name = "s", Kind = ValueKind.String } };
        var cons = new List<SolverConstraint>
        {
            new InConstraint { Param = "s", Values = { "foo", "bar", "baz" } },
            new PredicateConstraint { Expr = Bin("!=", Var("s"), Str("bar")) },
        };
        var res = new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);
        var vals = res.Select(r => r["s"]?.ToString()).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "baz", "foo" }, vals);
    }

    [Fact]
    public void Z3_StringEquality_LiteralAbsentFromDomain_MatchesNothing()
    {
        var ps = new List<SolverParam> { new() { Name = "s", Kind = ValueKind.String } };
        var cons = new List<SolverConstraint>
        {
            new InConstraint { Param = "s", Values = { "foo", "bar" } },
            new PredicateConstraint { Expr = Bin("==", Var("s"), Str("absent")) }, // index -1 → never matches
        };
        Assert.Empty(new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100));
    }
}
