using System.Collections.Generic;
using System.Linq;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for the Z3-backed parts of <c>SpecExplorerKit.Components.Solving</c> (the constraint
/// solver extracted from the vertical per ARC002). Exercises the Z3 self-test and the
/// <see cref="Z3Solver"/> across int/bool/string parameter kinds with In and predicate constraints.
/// </summary>
public class SolvingZ3Tests
{
    private static Dictionary<string, object?> A(params (string, object?)[] kv)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }

    private static VarExpr Var(string n) => new() { Name = n };
    private static LitExpr Lit(object? v) => new() { Value = v, Kind = ValueKind.Int };
    private static BinExpr Bin(string op, Expr l, Expr r) => new() { Op = op, Left = l, Right = r };

    [Fact]
    public void Z3Probe_SelfTest_Solves()
    {
        var report = Z3Probe.SelfTest();
        Assert.Contains("Microsoft.Z3", report);
        Assert.Contains("check=SATISFIABLE", report);
    }

    [Fact]
    public void Z3_Name_IsZ3()
    {
        Assert.Equal("z3", new Z3Solver().Name);
    }

    [Fact]
    public void Z3_IntInConstraints_ProduceCartesianProduct()
    {
        var solver = new Z3Solver();
        var ps = new List<SolverParam>
        {
            new() { Name = "a", Kind = ValueKind.Int },
            new() { Name = "b", Kind = ValueKind.Int },
        };
        var cons = new List<SolverConstraint>
        {
            new InConstraint { Param = "a", Values = { 1, 2 } },
            new InConstraint { Param = "b", Values = { 9 } },
        };
        var res = solver.Generate(ps, cons, new CombinationSpec(), 100);
        Assert.Equal(2, res.Count);
        Assert.All(res, r => Assert.Equal(9L, System.Convert.ToInt64(r["b"])));
    }

    [Fact]
    public void Z3_PredicateConstraint_Filters()
    {
        var solver = new Z3Solver();
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int, Domain = new object?[] { 1, 2, 3, 4 } } };
        var cons = new List<SolverConstraint> { new PredicateConstraint { Expr = Bin(">", Var("a"), Lit(2)) } };
        var res = solver.Generate(ps, cons, new CombinationSpec(), 100);
        var vals = res.Select(r => System.Convert.ToInt64(r["a"])).OrderBy(x => x).ToList();
        Assert.Equal(new long[] { 3, 4 }, vals);
    }

    [Fact]
    public void Z3_BoolParameter_EnumeratesBothValues()
    {
        var solver = new Z3Solver();
        var ps = new List<SolverParam> { new() { Name = "flag", Kind = ValueKind.Bool, Domain = new object?[] { true, false } } };
        var res = solver.Generate(ps, new List<SolverConstraint>(), new CombinationSpec(), 100);
        var vals = res.Select(r => System.Convert.ToBoolean(r["flag"])).OrderBy(x => x).ToList();
        Assert.Equal(new[] { false, true }, vals);
    }

    [Fact]
    public void Z3_StringDomain_ReturnsListedValues()
    {
        var solver = new Z3Solver();
        var ps = new List<SolverParam> { new() { Name = "s", Kind = ValueKind.String } };
        var cons = new List<SolverConstraint> { new InConstraint { Param = "s", Values = { "foo", "bar" } } };
        var res = solver.Generate(ps, cons, new CombinationSpec(), 100);
        var vals = res.Select(r => r["s"]?.ToString()).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "bar", "foo" }, vals);
    }

    [Fact]
    public void Z3_CompiledPredicate_AppliedAsPostFilter()
    {
        var solver = new Z3Solver();
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int, Domain = new object?[] { 1, 2, 3, 4 } } };
        var cons = new List<SolverConstraint>
        {
            new CompiledPredicateConstraint { Source = "even", Predicate = d => System.Convert.ToInt64(d["a"]) % 2 == 0 },
        };
        var res = solver.Generate(ps, cons, new CombinationSpec(), 100);
        Assert.All(res, r => Assert.Equal(0, System.Convert.ToInt64(r["a"]) % 2));
        Assert.Equal(2, res.Count); // 2 and 4
    }

    [Fact]
    public void Z3_Limit_IsRespected()
    {
        var solver = new Z3Solver();
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int, Domain = new object?[] { 1, 2, 3, 4, 5 } } };
        var res = solver.Generate(ps, new List<SolverConstraint>(), new CombinationSpec(), 3);
        Assert.Equal(3, res.Count);
    }
}
