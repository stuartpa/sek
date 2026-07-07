using System;
using System.Collections.Generic;
using System.Linq;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for the <see cref="Z3Solver"/> constraint translation: string-index equality,
/// boolean parameters, the arithmetic and bitwise operators in <c>TransInt</c>, and the
/// unsatisfiable path. These exercise translation arms the existing Z3 tests do not reach.
/// </summary>
public class SolvingZ3BranchCoverageTests
{
    private static VarExpr Var(string n) => new() { Name = n };
    private static LitExpr Int(long v) => new() { Value = v, Kind = ValueKind.Int };
    private static LitExpr Str(string v) => new() { Value = v, Kind = ValueKind.String };
    private static BinExpr Bin(string op, Expr l, Expr r) => new() { Op = op, Left = l, Right = r };

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> Solve(
        List<SolverParam> ps, List<SolverConstraint> cons) =>
        new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);

    [Fact]
    public void Z3_StringEquality_SelectsByIndex()
    {
        var ps = new List<SolverParam> { new() { Name = "s", Kind = ValueKind.String, Domain = new object?[] { "a", "b", "c" } } };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = Bin("==", Var("s"), Str("b")) } };
        var res = Solve(ps, cons);
        Assert.Single(res);
        Assert.Equal("b", res[0]["s"]?.ToString());
    }

    [Fact]
    public void Z3_StringEquality_LiteralOnLeft()
    {
        var ps = new List<SolverParam> { new() { Name = "s", Kind = ValueKind.String, Domain = new object?[] { "x", "y" } } };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = Bin("==", Str("y"), Var("s")) } };
        var res = Solve(ps, cons);
        Assert.Single(res);
        Assert.Equal("y", res[0]["s"]?.ToString());
    }

    [Fact]
    public void Z3_BoolParameter_Constrained()
    {
        var ps = new List<SolverParam> { new() { Name = "b", Kind = ValueKind.Bool, Domain = new object?[] { true, false } } };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = Var("b") } };
        var res = Solve(ps, cons);
        Assert.Contains(res, r => Convert.ToBoolean(r["b"]));
    }

    [Fact]
    public void Z3_Arithmetic_Operators()
    {
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int, Domain = Enumerable.Range(0, 10).Cast<object?>().ToArray() } };

        // a + 1 == 5  → a = 4
        Assert.Contains(Solve(ps, new() { new PredicateConstraint { Expr = Bin("==", Bin("+", Var("a"), Int(1)), Int(5)) } }),
            r => Convert.ToInt64(r["a"]) == 4);
        // a - 2 == 3  → a = 5
        Assert.Contains(Solve(ps, new() { new PredicateConstraint { Expr = Bin("==", Bin("-", Var("a"), Int(2)), Int(3)) } }),
            r => Convert.ToInt64(r["a"]) == 5);
        // a * 2 == 6  → a = 3
        Assert.Contains(Solve(ps, new() { new PredicateConstraint { Expr = Bin("==", Bin("*", Var("a"), Int(2)), Int(6)) } }),
            r => Convert.ToInt64(r["a"]) == 3);
        // a / 2 == 3  → a ∈ {6,7}
        Assert.Contains(Solve(ps, new() { new PredicateConstraint { Expr = Bin("==", Bin("/", Var("a"), Int(2)), Int(3)) } }),
            r => Convert.ToInt64(r["a"]) is 6 or 7);
        // a % 3 == 1  → a ∈ {1,4,7}
        Assert.Contains(Solve(ps, new() { new PredicateConstraint { Expr = Bin("==", Bin("%", Var("a"), Int(3)), Int(1)) } }),
            r => Convert.ToInt64(r["a"]) is 1 or 4 or 7);
        // unary minus: -a == -4 → a = 4
        Assert.Contains(Solve(ps, new() { new PredicateConstraint { Expr = Bin("==", new UnExpr { Op = "-", Operand = Var("a") }, Int(-4)) } }),
            r => Convert.ToInt64(r["a"]) == 4);
    }

    [Fact]
    public void Z3_Bitwise_Operators()
    {
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int, Domain = Enumerable.Range(0, 8).Cast<object?>().ToArray() } };
        // (a & 1) == 1  → odd values
        var andRes = Solve(ps, new() { new PredicateConstraint { Expr = Bin("==", Bin("&", Var("a"), Int(1)), Int(1)) } });
        Assert.All(andRes, r => Assert.True(Convert.ToInt64(r["a"]) % 2 == 1));
        // (a | 1) == 1  → a ∈ {0,1}
        var orRes = Solve(ps, new() { new PredicateConstraint { Expr = Bin("==", Bin("|", Var("a"), Int(1)), Int(1)) } });
        Assert.All(orRes, r => Assert.True(Convert.ToInt64(r["a"]) is 0 or 1));
    }

    [Fact]
    public void Z3_Unsatisfiable_YieldsNothing()
    {
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int, Domain = new object?[] { 1, 2, 3 } } };
        var cons = new List<SolverConstraint>
        {
            new PredicateConstraint { Expr = Bin("&&", Bin(">", Var("a"), Int(5)), Bin("<", Var("a"), Int(1))) },
        };
        Assert.Empty(Solve(ps, cons));
    }
}
