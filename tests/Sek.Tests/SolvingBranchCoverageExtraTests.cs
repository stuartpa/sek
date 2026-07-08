using System;
using System.Collections.Generic;
using System.Linq;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Extra branch coverage for the Solving component: the boolean/comparison translation arms
/// of <see cref="Z3Solver"/> (bool literals, negation, <c>||</c>, single-char <c>&amp;</c>/<c>|</c>
/// in a boolean context, <c>&lt;=</c>/<c>&gt;=</c>, string <c>!=</c>, absent-string index, <c>long</c>
/// literals) and the C#-side <see cref="PredicateEval"/> arms exercised by the
/// <see cref="EnumerativeSolver"/> (boolean/numeric bitwise ops, string/bool equality, div/mod by
/// zero guards, the unsupported-op default).
/// </summary>
public class SolvingBranchCoverageExtraTests
{
    private static VarExpr Var(string n) => new() { Name = n };
    private static LitExpr Int(long v) => new() { Value = v, Kind = ValueKind.Int };
    private static LitExpr Long(long v) => new() { Value = v, Kind = ValueKind.Long };
    private static LitExpr Str(string v) => new() { Value = v, Kind = ValueKind.String };
    private static LitExpr Bool(bool v) => new() { Value = v, Kind = ValueKind.Bool };
    private static UnExpr Un(string op, Expr e) => new() { Op = op, Operand = e };
    private static BinExpr Bin(string op, Expr l, Expr r) => new() { Op = op, Left = l, Right = r };

    private static SolverParam IntP(string n, params object?[] domain) =>
        new() { Name = n, Kind = ValueKind.Int, Domain = domain };

    private static SolverParam BoolP(string n) =>
        new() { Name = n, Kind = ValueKind.Bool, Domain = new object?[] { true, false } };

    private static SolverParam StrP(string n, params object?[] domain) =>
        new() { Name = n, Kind = ValueKind.String, Domain = domain };

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> Z3(
        List<SolverParam> ps, params SolverConstraint[] cons) =>
        new Z3Solver().Generate(ps, cons.ToList(), new CombinationSpec(), 100);

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> Enum(
        List<SolverParam> ps, params SolverConstraint[] cons) =>
        new EnumerativeSolver().Generate(ps, cons.ToList(), new CombinationSpec(), 100);

    private static PredicateConstraint Pred(Expr e) => new() { Expr = e };

    // ---- Z3 boolean / comparison translation arms --------------------------------

    [Fact]
    public void Z3_BoolLiteralPredicate_TrueKeepsAll_FalseDropsAll()
    {
        var ps = new List<SolverParam> { IntP("a", 1, 2, 3) };
        Assert.Equal(3, Z3(ps, Pred(Bool(true))).Count);
        Assert.Empty(Z3(ps, Pred(Bool(false))));
    }

    [Fact]
    public void Z3_NegatedBoolVar_SelectsFalse()
    {
        var res = Z3(new List<SolverParam> { BoolP("b") }, Pred(Un("!", Var("b"))));
        Assert.Single(res);
        Assert.False(Convert.ToBoolean(res[0]["b"]));
    }

    [Fact]
    public void Z3_BoolOr_And_SingleCharBoolOps()
    {
        var ps = new List<SolverParam> { BoolP("b"), BoolP("c") };

        // b || c  -> every assignment except (false,false)
        var orRes = Z3(ps, Pred(Bin("||", Var("b"), Var("c"))));
        Assert.Equal(3, orRes.Count);

        // single-char & in a boolean context (left is a LitExpr bool) -> true & c
        var andRes = Z3(ps, Pred(Bin("&", Bool(true), Var("c"))));
        Assert.All(andRes, r => Assert.True(Convert.ToBoolean(r["c"])));

        // single-char | in a boolean context (right is a UnExpr) -> b | !c
        var mixRes = Z3(ps, Pred(Bin("|", Var("b"), Un("!", Var("c")))));
        Assert.Contains(mixRes, r => Convert.ToBoolean(r["b"]));

        // single-char & where left is a BinExpr comparison -> (b==true) & c
        var cmpRes = Z3(ps, Pred(Bin("&", Bin("==", Var("b"), Bool(true)), Var("c"))));
        Assert.All(cmpRes, r => Assert.True(Convert.ToBoolean(r["b"]) && Convert.ToBoolean(r["c"])));
    }

    [Fact]
    public void Z3_LessOrEqual_GreaterOrEqual_And_NotEqualInt()
    {
        var ps = new List<SolverParam> { IntP("a", 0, 1, 2, 3, 4) };

        Assert.All(Z3(ps, Pred(Bin("<=", Var("a"), Int(1)))), r => Assert.True(Convert.ToInt64(r["a"]) <= 1));
        Assert.All(Z3(ps, Pred(Bin(">=", Var("a"), Int(3)))), r => Assert.True(Convert.ToInt64(r["a"]) >= 3));
        Assert.All(Z3(ps, Pred(Bin("!=", Var("a"), Int(2)))), r => Assert.True(Convert.ToInt64(r["a"]) != 2));
    }

    [Fact]
    public void Z3_LongLiteral_Comparison()
    {
        var ps = new List<SolverParam> { IntP("a", 0, 1, 2, 3, 4) };
        var res = Z3(ps, Pred(Bin("==", Var("a"), Long(4))));
        Assert.Single(res);
        Assert.Equal(4, Convert.ToInt64(res[0]["a"]));
    }

    [Fact]
    public void Z3_StringNotEqual_And_AbsentLiteralIsUnsatisfiable()
    {
        var ps = new List<SolverParam> { StrP("s", "a", "b", "c") };

        var neq = Z3(ps, Pred(Bin("!=", Var("s"), Str("b"))));
        Assert.All(neq, r => Assert.NotEqual("b", r["s"]?.ToString()));

        // literal not present in the domain -> index -1 -> never matches
        Assert.Empty(Z3(ps, Pred(Bin("==", Var("s"), Str("zzz")))));
    }

    // ---- PredicateEval arms (via the enumerative solver) -------------------------

    [Fact]
    public void Enum_BooleanBitwiseAnd_Or()
    {
        var ps = new List<SolverParam> { BoolP("b"), BoolP("c") };

        var and = Enum(ps, Pred(Bin("&", Var("b"), Var("c"))));
        Assert.All(and, r => Assert.True(Convert.ToBoolean(r["b"]) && Convert.ToBoolean(r["c"])));

        var or = Enum(ps, Pred(Bin("|", Var("b"), Var("c"))));
        Assert.All(or, r => Assert.True(Convert.ToBoolean(r["b"]) || Convert.ToBoolean(r["c"])));
    }

    [Fact]
    public void Enum_NumericBitwiseAnd_Or()
    {
        var ps = new List<SolverParam> { IntP("a", 0, 1, 2, 3, 4, 5, 6, 7) };

        var and = Enum(ps, Pred(Bin("==", Bin("&", Var("a"), Int(1)), Int(1))));
        Assert.All(and, r => Assert.True(Convert.ToInt64(r["a"]) % 2 == 1));

        var or = Enum(ps, Pred(Bin("==", Bin("|", Var("a"), Int(1)), Int(1))));
        Assert.All(or, r => Assert.True(Convert.ToInt64(r["a"]) is 0 or 1));
    }

    [Fact]
    public void Enum_StringEquality_And_Inequality()
    {
        var ps = new List<SolverParam> { StrP("s", "x", "y", "z") };

        var eq = Enum(ps, Pred(Bin("==", Var("s"), Str("y"))));
        Assert.Single(eq);
        Assert.Equal("y", eq[0]["s"]?.ToString());

        var neq = Enum(ps, Pred(Bin("!=", Var("s"), Str("y"))));
        Assert.Equal(2, neq.Count);
    }

    [Fact]
    public void Enum_BooleanEquality()
    {
        var ps = new List<SolverParam> { BoolP("b") };
        var res = Enum(ps, Pred(Bin("==", Var("b"), Bool(true))));
        Assert.Single(res);
        Assert.True(Convert.ToBoolean(res[0]["b"]));
    }

    [Fact]
    public void Enum_NumericComparisons_And_UnaryMinus()
    {
        var ps = new List<SolverParam> { IntP("a", -2, -1, 0, 1, 2) };

        Assert.All(Enum(ps, Pred(Bin("<=", Var("a"), Int(0)))), r => Assert.True(Convert.ToInt64(r["a"]) <= 0));
        Assert.All(Enum(ps, Pred(Bin(">=", Var("a"), Int(0)))), r => Assert.True(Convert.ToInt64(r["a"]) >= 0));
        // unary minus: -a > 0  -> a < 0
        Assert.All(Enum(ps, Pred(Bin(">", Un("-", Var("a")), Int(0)))), r => Assert.True(Convert.ToInt64(r["a"]) < 0));
    }

    [Fact]
    public void Enum_DivAndModByZero_GuardYieldsZero()
    {
        var ps = new List<SolverParam> { IntP("a", 1, 2, 3) };
        // a / 0 == 0 (guarded to 0) -> all rows satisfy
        Assert.Equal(3, Enum(ps, Pred(Bin("==", Bin("/", Var("a"), Int(0)), Int(0)))).Count);
        // a % 0 == 0 (guarded to 0) -> all rows satisfy
        Assert.Equal(3, Enum(ps, Pred(Bin("==", Bin("%", Var("a"), Int(0)), Int(0)))).Count);
    }

    [Fact]
    public void Enum_ArithmeticOperators()
    {
        var ps = new List<SolverParam> { IntP("a", 0, 1, 2, 3, 4, 5) };
        Assert.Contains(Enum(ps, Pred(Bin("==", Bin("+", Var("a"), Int(1)), Int(3)))), r => Convert.ToInt64(r["a"]) == 2);
        Assert.Contains(Enum(ps, Pred(Bin("==", Bin("-", Var("a"), Int(1)), Int(1)))), r => Convert.ToInt64(r["a"]) == 2);
        Assert.Contains(Enum(ps, Pred(Bin("==", Bin("*", Var("a"), Int(2)), Int(4)))), r => Convert.ToInt64(r["a"]) == 2);
    }

    [Fact]
    public void Enum_UnsupportedOperator_DropsAllRows()
    {
        var ps = new List<SolverParam> { IntP("a", 1, 2, 3) };
        // "^" is not modelled by PredicateEval -> evaluates to null -> predicate is false for all.
        Assert.Empty(Enum(ps, Pred(Bin("^", Var("a"), Int(1)))));
    }
}
