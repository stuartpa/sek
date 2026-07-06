using System;
using System.Collections.Generic;
using System.Linq;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Extra targeted coverage for <c>SpecExplorerKit.Components.Solving</c> — the Z3 predicate
/// translation paths (comparison / arithmetic / bitwise / logical / string / enum), the
/// combinatorial reducer's column/isolated paths, and the Roslyn predicate preambles — to bring
/// the extracted component to the ≥95% component bar.
/// </summary>
public class SolvingExtraTests
{
    private enum Color { Red = 0, Green = 1, Blue = 2 }

    private static Dictionary<string, object?> A(params (string, object?)[] kv)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }

    private static VarExpr Var(string n) => new() { Name = n };
    private static LitExpr Int(long v) => new() { Value = v, Kind = ValueKind.Int };
    private static LitExpr Str(string v) => new() { Value = v, Kind = ValueKind.String };
    private static LitExpr Bool(bool v) => new() { Value = v, Kind = ValueKind.Bool };
    private static BinExpr Bin(string op, Expr l, Expr r) => new() { Op = op, Left = l, Right = r };
    private static UnExpr Un(string op, Expr o) => new() { Op = op, Operand = o };

    private static List<long> Z3Ints(string name, object?[] domain, Expr predicate)
    {
        var ps = new List<SolverParam> { new() { Name = name, Kind = ValueKind.Int, Domain = domain } };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = predicate } };
        return new Z3Solver().Generate(ps, cons, new CombinationSpec(), 1000)
            .Select(r => Convert.ToInt64(r[name])).OrderBy(x => x).ToList();
    }

    private static readonly object?[] OneToSix = { 1L, 2L, 3L, 4L, 5L, 6L };

    // ---- Z3 comparison operators -------------------------------------------------------

    [Fact] public void Z3_LessThan() => Assert.Equal(new long[] { 1, 2 }, Z3Ints("a", OneToSix, Bin("<", Var("a"), Int(3))));
    [Fact] public void Z3_LessEqual() => Assert.Equal(new long[] { 1, 2, 3 }, Z3Ints("a", OneToSix, Bin("<=", Var("a"), Int(3))));
    [Fact] public void Z3_GreaterEqual() => Assert.Equal(new long[] { 5, 6 }, Z3Ints("a", OneToSix, Bin(">=", Var("a"), Int(5))));
    [Fact] public void Z3_EqualAndNotEqual()
    {
        Assert.Equal(new long[] { 3 }, Z3Ints("a", OneToSix, Bin("==", Var("a"), Int(3))));
        Assert.Equal(new long[] { 1, 2, 4, 5, 6 }, Z3Ints("a", OneToSix, Bin("!=", Var("a"), Int(3))));
    }

    // ---- Z3 arithmetic & bitwise in predicates -----------------------------------------

    [Fact] public void Z3_Modulo() => Assert.Equal(new long[] { 2, 4, 6 }, Z3Ints("a", OneToSix, Bin("==", Bin("%", Var("a"), Int(2)), Int(0))));
    [Fact] public void Z3_Addition() => Assert.Equal(new long[] { 4 }, Z3Ints("a", OneToSix, Bin("==", Bin("+", Var("a"), Int(1)), Int(5))));
    [Fact] public void Z3_Subtraction() => Assert.Equal(new long[] { 6 }, Z3Ints("a", OneToSix, Bin("==", Bin("-", Var("a"), Int(1)), Int(5))));
    [Fact] public void Z3_Multiplication() => Assert.Equal(new long[] { 3 }, Z3Ints("a", OneToSix, Bin("==", Bin("*", Var("a"), Int(2)), Int(6))));
    [Fact] public void Z3_Division() => Assert.Equal(new long[] { 4, 5 }, Z3Ints("a", OneToSix, Bin("==", Bin("/", Var("a"), Int(2)), Int(2))));
    [Fact] public void Z3_BitwiseAnd() => Assert.Equal(new long[] { 2, 3, 6 }, Z3Ints("a", OneToSix, Bin("==", Bin("&", Var("a"), Int(2)), Int(2))));
    [Fact] public void Z3_BitwiseOr() => Assert.Equal(new long[] { 1, 3, 5 }, Z3Ints("a", OneToSix, Bin("==", Bin("|", Var("a"), Int(1)), Var("a"))));
    [Fact] public void Z3_UnaryMinus() => Assert.Equal(new long[] { 3 }, Z3Ints("a", OneToSix, Bin("==", Un("-", Var("a")), Int(-3))));

    // ---- Z3 logical connectives --------------------------------------------------------

    [Fact] public void Z3_LogicalAnd() => Assert.Equal(new long[] { 3, 4 }, Z3Ints("a", OneToSix, Bin("&&", Bin(">", Var("a"), Int(2)), Bin("<", Var("a"), Int(5)))));
    [Fact] public void Z3_LogicalOr() => Assert.Equal(new long[] { 1, 6 }, Z3Ints("a", OneToSix, Bin("||", Bin("<", Var("a"), Int(2)), Bin(">", Var("a"), Int(5)))));
    [Fact] public void Z3_Not() => Assert.Equal(new long[] { 1, 2, 4, 5, 6 }, Z3Ints("a", OneToSix, Un("!", Bin("==", Var("a"), Int(3)))));

    [Fact]
    public void Z3_Unsatisfiable_YieldsEmpty()
    {
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int } };
        var cons = new List<SolverConstraint>
        {
            new InConstraint { Param = "a", Values = { 1 } },
            new PredicateConstraint { Expr = Bin(">", Var("a"), Int(5)) },
        };
        Assert.Empty(new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100));
    }

    [Fact]
    public void Z3_StringEquality_Predicate()
    {
        var ps = new List<SolverParam> { new() { Name = "s", Kind = ValueKind.String } };
        var cons = new List<SolverConstraint>
        {
            new InConstraint { Param = "s", Values = { "foo", "bar", "baz" } },
            new PredicateConstraint { Expr = Bin("==", Var("s"), Str("bar")) },
        };
        var res = new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);
        Assert.Single(res);
        Assert.Equal("bar", res[0]["s"]);
    }

    [Fact]
    public void Z3_BoolVar_Predicate()
    {
        var ps = new List<SolverParam> { new() { Name = "flag", Kind = ValueKind.Bool, Domain = new object?[] { true, false } } };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = Var("flag") } };
        var res = new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);
        Assert.Single(res);
        Assert.True(Convert.ToBoolean(res[0]["flag"]));
    }

    [Fact]
    public void Z3_EnumAsInt_MapsBackToEnum()
    {
        var ps = new List<SolverParam> { new() { Name = "c", Kind = ValueKind.Int, Domain = new object?[] { Color.Red, Color.Green, Color.Blue } } };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = Bin(">=", Var("c"), Int(1)) } };
        var res = new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100);
        var vals = res.Select(r => r["c"]).ToList();
        Assert.Contains(Color.Green, vals);
        Assert.Contains(Color.Blue, vals);
        Assert.DoesNotContain(Color.Red, vals);
    }

    // ---- Combinatorics column / isolated paths -----------------------------------------

    [Fact]
    public void Combinatorics_Apply_PairwiseColumns_OverDerivedValues()
    {
        var names = new List<string> { "days" };
        var all = new List<IReadOnlyDictionary<string, object?>>();
        for (var d = 0; d < 8; d++) all.Add(A(("days", (long)d)));
        var spec = new CombinationSpec();
        spec.PairwiseColumns.Add(("lo", Bin("&", Var("days"), Int(1))));
        spec.PairwiseColumns.Add(("hi", Bin("&", Var("days"), Int(2))));
        var res = Combinatorics.Apply(names, all, spec, 100);
        Assert.NotEmpty(res);
        Assert.True(res.Count <= all.Count);
    }

    [Fact]
    public void Combinatorics_Apply_Isolated_KeepsOneRepresentative()
    {
        var names = new List<string> { "a" };
        var all = new List<IReadOnlyDictionary<string, object?>> { A(("a", 1L)), A(("a", 2L)), A(("a", 3L)) };
        var spec = new CombinationSpec();
        spec.Isolated.Add(Bin(">", Var("a"), Int(1))); // a=2 and a=3 both match; keep only one
        var res = Combinatorics.Apply(names, all, spec, 100);
        Assert.Single(res, r => Convert.ToInt64(r["a"]) > 1);
    }

    [Fact]
    public void Combinatorics_Apply_PairwiseMode_ReducesRows()
    {
        var names = new List<string> { "a", "b" };
        var all = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var a in new[] { 0, 1 })
        foreach (var b in new[] { 0, 1 })
            all.Add(A(("a", a), ("b", b)));
        var spec = new CombinationSpec { Mode = CombinationSpec.Strategy.Pairwise };
        var res = Combinatorics.Apply(names, all, spec, 100);
        Assert.True(res.Count <= all.Count);
    }

    // ---- RoslynPredicate preambles / failure -------------------------------------------

    [Fact]
    public void Roslyn_BoolParameter_Preamble()
    {
        var pred = RoslynPredicate.TryCompile("flag", new List<SolverParam> { new() { Name = "flag", Kind = ValueKind.Bool } });
        Assert.NotNull(pred);
        Assert.True(pred!(A(("flag", true))));
        Assert.False(pred(A(("flag", false))));
    }

    [Fact]
    public void Roslyn_LongParameter_Preamble()
    {
        var pred = RoslynPredicate.TryCompile("n > 10", new List<SolverParam> { new() { Name = "n", Kind = ValueKind.Long } });
        Assert.NotNull(pred);
        Assert.True(pred!(A(("n", 11L))));
        Assert.False(pred(A(("n", 5L))));
    }

    [Fact]
    public void Roslyn_InvalidExpression_ReturnsNull()
    {
        var pred = RoslynPredicate.TryCompile("this is not valid c#", new List<SolverParam> { new() { Name = "x", Kind = ValueKind.Int } });
        Assert.Null(pred);
    }

    [Fact]
    public void Roslyn_RuntimeError_CountsAsFalse()
    {
        // integer division by zero throws at runtime → treated as "does not satisfy"
        var pred = RoslynPredicate.TryCompile("(100 / x) == 0", new List<SolverParam> { new() { Name = "x", Kind = ValueKind.Int } });
        Assert.NotNull(pred);
        Assert.False(pred!(A(("x", 0))));
    }
}
