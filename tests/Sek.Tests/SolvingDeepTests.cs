using System;
using System.Collections.Generic;
using System.Linq;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Deeper Z3 translation-branch coverage for <c>SpecExplorerKit.Components.Solving</c>: bitwise-as-
/// logical connectives over bool params, bool literals in predicates, two-string-variable equality
/// via the string-index path, seeded re-insertion after a pairwise reduction, and numeric coercion
/// of a missing variable.
/// </summary>
public class SolvingDeepTests
{
    private static Dictionary<string, object?> A(params (string, object?)[] kv)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }

    private static VarExpr Var(string n) => new() { Name = n };
    private static LitExpr Int(long v) => new() { Value = v, Kind = ValueKind.Int };
    private static LitExpr Bool(bool v) => new() { Value = v, Kind = ValueKind.Bool };
    private static BinExpr Bin(string op, Expr l, Expr r) => new() { Op = op, Left = l, Right = r };

    private static readonly object?[] TF = { true, false };

    [Fact]
    public void Z3_BitwiseAnd_AsLogical_OverBoolVars()
    {
        var ps = new List<SolverParam>
        {
            new() { Name = "x", Kind = ValueKind.Bool, Domain = TF },
            new() { Name = "y", Kind = ValueKind.Bool, Domain = TF },
        };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = Bin("&", Var("x"), Var("y")) } };
        var res = new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);
        Assert.Single(res);
        Assert.True(Convert.ToBoolean(res[0]["x"]) && Convert.ToBoolean(res[0]["y"]));
    }

    [Fact]
    public void Z3_BitwiseOr_AsLogical_OverBoolVars()
    {
        var ps = new List<SolverParam>
        {
            new() { Name = "x", Kind = ValueKind.Bool, Domain = TF },
            new() { Name = "y", Kind = ValueKind.Bool, Domain = TF },
        };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = Bin("|", Var("x"), Var("y")) } };
        var res = new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);
        Assert.Equal(3, res.Count); // all except (false,false)
    }

    [Fact]
    public void Z3_BoolLiteral_InPredicate()
    {
        var ps = new List<SolverParam> { new() { Name = "flag", Kind = ValueKind.Bool, Domain = TF } };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = Bin("&&", Var("flag"), Bool(true)) } };
        var res = new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);
        Assert.Single(res);
        Assert.True(Convert.ToBoolean(res[0]["flag"]));
    }

    [Fact]
    public void Z3_TwoStringVars_Equality()
    {
        var ps = new List<SolverParam>
        {
            new() { Name = "s1", Kind = ValueKind.String },
            new() { Name = "s2", Kind = ValueKind.String },
        };
        var cons = new List<SolverConstraint>
        {
            new InConstraint { Param = "s1", Values = { "a", "b" } },
            new InConstraint { Param = "s2", Values = { "a", "b" } },
            new PredicateConstraint { Expr = Bin("==", Var("s1"), Var("s2")) },
        };
        var res = new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);
        Assert.All(res, r => Assert.Equal(r["s1"], r["s2"]));
        Assert.Equal(2, res.Count); // (a,a) and (b,b)
    }

    [Fact]
    public void Combinatorics_Seeded_ReinsertsTripleDroppedByPairwise()
    {
        var names = new List<string> { "a", "b", "c" };
        var all = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var a in new[] { 0, 1 })
        foreach (var b in new[] { 0, 1 })
        foreach (var c in new[] { 0, 1 })
            all.Add(A(("a", a), ("b", b), ("c", c)));
        var spec = new CombinationSpec { Mode = CombinationSpec.Strategy.Pairwise };
        spec.Seeded.Add(new List<Expr> { Bin("==", Var("a"), Int(1)), Bin("==", Var("b"), Int(1)), Bin("==", Var("c"), Int(1)) });
        var res = Combinatorics.Apply(names, all, spec, 100);
        Assert.Contains(res, r => Convert.ToInt64(r["a"]) == 1 && Convert.ToInt64(r["b"]) == 1 && Convert.ToInt64(r["c"]) == 1);
    }

    [Fact]
    public void PredicateEval_MissingVar_CoercesToZero_InComparison()
    {
        // ToLong(null) == 0, so a missing variable compares as 0.
        Assert.True(PredicateEval.Eval(Bin("<", Var("missing"), Int(3)), A()));
        Assert.False(PredicateEval.Eval(Bin(">", Var("missing"), Int(3)), A()));
    }
}
