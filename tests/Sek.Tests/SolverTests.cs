using System.Collections.Generic;
using System.Linq;
using Sek.Solver;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for <c>Sek.Solver</c>: the predicate evaluator, the dependency-free enumerative
/// solver, the combinatorial reducer, and the Roslyn predicate compiler. These were almost
/// entirely uncovered (~1.4% line) before this suite (readiness-gate coverage drive).
/// </summary>
public class SolverTests
{
    private static Dictionary<string, object?> A(params (string, object?)[] kv)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }

    private static VarExpr Var(string n) => new() { Name = n };
    private static LitExpr Lit(object? v, ValueKind k = ValueKind.Int) => new() { Value = v, Kind = k };
    private static BinExpr Bin(string op, Expr l, Expr r) => new() { Op = op, Left = l, Right = r };

    // ---- PredicateEval -----------------------------------------------------------------

    [Theory]
    [InlineData("==", 5, 5, true)]
    [InlineData("==", 5, 6, false)]
    [InlineData("!=", 5, 6, true)]
    [InlineData("<", 4, 5, true)]
    [InlineData("<=", 5, 5, true)]
    [InlineData(">", 6, 5, true)]
    [InlineData(">=", 5, 6, false)]
    public void Eval_Comparisons(string op, int l, int r, bool expected)
    {
        Assert.Equal(expected, PredicateEval.Eval(Bin(op, Lit(l), Lit(r)), A()));
    }

    [Theory]
    [InlineData("+", 2, 3, 5L)]
    [InlineData("-", 7, 3, 4L)]
    [InlineData("*", 4, 3, 12L)]
    [InlineData("/", 12, 4, 3L)]
    [InlineData("%", 13, 5, 3L)]
    [InlineData("&", 6, 3, 2L)]
    [InlineData("|", 4, 1, 5L)]
    public void Evaluate_Arithmetic_And_Bitwise(string op, int l, int r, long expected)
    {
        Assert.Equal(expected, PredicateEval.Evaluate(Bin(op, Lit(l), Lit(r)), A()));
    }

    [Fact]
    public void Evaluate_DivideAndModByZero_AreSafeZero()
    {
        Assert.Equal(0L, PredicateEval.Evaluate(Bin("/", Lit(5), Lit(0)), A()));
        Assert.Equal(0L, PredicateEval.Evaluate(Bin("%", Lit(5), Lit(0)), A()));
    }

    [Fact]
    public void Eval_LogicalAndOr_ShortAndLong()
    {
        Assert.True(PredicateEval.Eval(Bin("&&", Lit(true, ValueKind.Bool), Lit(true, ValueKind.Bool)), A()));
        Assert.False(PredicateEval.Eval(Bin("&&", Lit(true, ValueKind.Bool), Lit(false, ValueKind.Bool)), A()));
        Assert.True(PredicateEval.Eval(Bin("||", Lit(false, ValueKind.Bool), Lit(true, ValueKind.Bool)), A()));
        // & / | with a bool operand behave as logical
        Assert.True(PredicateEval.Eval(Bin("|", Lit(false, ValueKind.Bool), Lit(true, ValueKind.Bool)), A()));
    }

    [Fact]
    public void Eval_Unary_NotAndNegate()
    {
        Assert.True(PredicateEval.Eval(new UnExpr { Op = "!", Operand = Lit(false, ValueKind.Bool) }, A()));
        Assert.Equal(-5L, PredicateEval.Evaluate(new UnExpr { Op = "-", Operand = Lit(5) }, A()));
    }

    [Fact]
    public void Eval_Var_ResolvesFromAssignment_AndMissingIsNull()
    {
        Assert.True(PredicateEval.Eval(Bin("==", Var("x"), Lit(3)), A(("x", 3))));
        Assert.False(PredicateEval.Eval(Bin("==", Var("missing"), Lit(3)), A()));
    }

    [Fact]
    public void Eval_Equality_AcrossTypes()
    {
        Assert.True(PredicateEval.Eval(Bin("==", Var("s"), Lit("hi", ValueKind.String)), A(("s", "hi"))));
        Assert.True(PredicateEval.Eval(Bin("==", Var("b"), Lit(true, ValueKind.Bool)), A(("b", true))));
        Assert.True(PredicateEval.Eval(Bin("==", Var("n"), Lit(null)), A(("n", null))));
    }

    // ---- EnumerativeSolver -------------------------------------------------------------

    [Fact]
    public void Enumerative_InConstraints_ProduceCartesianProduct()
    {
        var solver = new EnumerativeSolver();
        var ps = new List<SolverParam> { new() { Name = "a" }, new() { Name = "b" } };
        var cons = new List<SolverConstraint>
        {
            new InConstraint { Param = "a", Values = { 1, 2 } },
            new InConstraint { Param = "b", Values = { 9 } },
        };
        var res = solver.Generate(ps, cons, new CombinationSpec(), 100);
        Assert.Equal(2, res.Count);
        Assert.All(res, r => Assert.Equal(9, r["b"]));
    }

    [Fact]
    public void Enumerative_PredicateConstraint_Filters()
    {
        var solver = new EnumerativeSolver();
        var ps = new List<SolverParam> { new() { Name = "a", Domain = new object?[] { 1, 2, 3, 4 } } };
        var cons = new List<SolverConstraint>
        {
            new PredicateConstraint { Expr = Bin(">", Var("a"), Lit(2)) },
        };
        var res = solver.Generate(ps, cons, new CombinationSpec(), 100);
        Assert.Equal(new[] { 3, 4 }, res.Select(r => (int)r["a"]!).OrderBy(x => x));
    }

    [Fact]
    public void Enumerative_CompiledPredicate_Filters()
    {
        var solver = new EnumerativeSolver();
        var ps = new List<SolverParam> { new() { Name = "a", Domain = new object?[] { 1, 2, 3 } } };
        var cons = new List<SolverConstraint>
        {
            new CompiledPredicateConstraint { Source = "a==2", Predicate = d => Equals(d["a"], 2) },
        };
        var res = solver.Generate(ps, cons, new CombinationSpec(), 100);
        Assert.Single(res);
        Assert.Equal(2, res[0]["a"]);
    }

    [Fact]
    public void Enumerative_EmptyDomain_YieldsNothing()
    {
        var solver = new EnumerativeSolver();
        var ps = new List<SolverParam> { new() { Name = "a" } };
        var res = solver.Generate(ps, new List<SolverConstraint>(), new CombinationSpec(), 100);
        Assert.Empty(res);
    }

    [Fact]
    public void Enumerative_NoParameters_YieldsSingleEmptyCombo()
    {
        var solver = new EnumerativeSolver();
        var res = solver.Generate(new List<SolverParam>(), new List<SolverConstraint>(), new CombinationSpec(), 100);
        Assert.Single(res);
        Assert.Empty(res[0]);
    }

    [Fact]
    public void Enumerative_Limit_IsRespected()
    {
        var solver = new EnumerativeSolver();
        var ps = new List<SolverParam> { new() { Name = "a", Domain = new object?[] { 1, 2, 3, 4, 5 } } };
        var res = solver.Generate(ps, new List<SolverConstraint>(), new CombinationSpec(), 3);
        Assert.Equal(3, res.Count);
    }

    [Fact]
    public void Enumerative_Name_IsEnumerative()
    {
        Assert.Equal("enumerative", new EnumerativeSolver().Name);
    }

    // ---- Combinatorics -----------------------------------------------------------------

    [Fact]
    public void Combinatorics_Pairwise_CoversAllValuePairs_WithFewerRows()
    {
        var names = new List<string> { "a", "b", "c" };
        var all = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var a in new[] { 0, 1 })
        foreach (var b in new[] { 0, 1 })
        foreach (var c in new[] { 0, 1 })
            all.Add(A(("a", a), ("b", b), ("c", c)));

        var reduced = Combinatorics.Pairwise(names, all);
        Assert.True(reduced.Count <= all.Count);

        // every value-pair present in the full set is still present in the reduced set
        string PairKey(string p, object? pv, string q, object? qv) => $"{p}={pv}|{q}={qv}";
        var required = new HashSet<string>();
        foreach (var combo in all)
            for (var i = 0; i < names.Count; i++)
                for (var j = i + 1; j < names.Count; j++)
                    required.Add(PairKey(names[i], combo[names[i]], names[j], combo[names[j]]));
        var covered = new HashSet<string>();
        foreach (var combo in reduced)
            for (var i = 0; i < names.Count; i++)
                for (var j = i + 1; j < names.Count; j++)
                    covered.Add(PairKey(names[i], combo[names[i]], names[j], combo[names[j]]));
        Assert.True(covered.SetEquals(required));
    }

    [Fact]
    public void Combinatorics_Apply_Expand_EnsuresEveryValueTuplePresent()
    {
        var names = new List<string> { "a" };
        var all = new List<IReadOnlyDictionary<string, object?>>
        {
            A(("a", 1)), A(("a", 2)), A(("a", 3)),
        };
        var spec = new CombinationSpec();
        spec.Expand.Add("a");
        var res = Combinatorics.Apply(names, all, spec, 100);
        Assert.Equal(new[] { 1, 2, 3 }, res.Select(r => (int)r["a"]!).OrderBy(x => x));
    }

    [Fact]
    public void Combinatorics_Apply_Seeded_GuaranteesConjunctionPresent()
    {
        var names = new List<string> { "a" };
        var all = new List<IReadOnlyDictionary<string, object?>> { A(("a", 1)), A(("a", 2)) };
        var spec = new CombinationSpec();
        spec.Seeded.Add(new List<Expr> { Bin("==", Var("a"), Lit(2)) });
        var res = Combinatorics.Apply(names, all, spec, 100);
        Assert.Contains(res, r => Equals(r["a"], 2));
    }

    // ---- RoslynPredicate ---------------------------------------------------------------

    [Fact]
    public void Roslyn_CompilesAndEvaluatesExpression()
    {
        var ps = new List<SolverParam> { new() { Name = "x", Kind = ValueKind.Int } };
        var pred = RoslynPredicate.TryCompile("x % 2 == 0", ps);
        Assert.NotNull(pred);
        Assert.True(pred!(A(("x", 4))));
        Assert.False(pred(A(("x", 5))));
    }

    [Fact]
    public void Roslyn_EmptyExpression_ReturnsNull()
    {
        Assert.Null(RoslynPredicate.TryCompile("   ", new List<SolverParam>()));
    }

    [Fact]
    public void Roslyn_StringMembers_Work()
    {
        var ps = new List<SolverParam> { new() { Name = "s", Kind = ValueKind.String } };
        var pred = RoslynPredicate.TryCompile("s.StartsWith(\"a\")", ps);
        Assert.NotNull(pred);
        Assert.True(pred!(A(("s", "abc"))));
        Assert.False(pred(A(("s", "xyz"))));
    }
}
