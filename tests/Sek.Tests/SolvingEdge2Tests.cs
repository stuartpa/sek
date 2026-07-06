using System;
using System.Collections.Generic;
using System.Linq;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>Last reachable Z3 branches: an unknown arithmetic operator inside an int comparison
/// (inner translation throw → post-filter) and an unconstrained boolean parameter (no candidate
/// clause added → Z3 enumerates both values).</summary>
public class SolvingEdge2Tests
{
    private static VarExpr Var(string n) => new() { Name = n };
    private static LitExpr Int(long v) => new() { Value = v, Kind = ValueKind.Int };
    private static BinExpr Bin(string op, Expr l, Expr r) => new() { Op = op, Left = l, Right = r };

    [Fact]
    public void Z3_UnknownArithmeticOp_FallsBackToPostFilter()
    {
        var ps = new List<SolverParam> { new() { Name = "a", Kind = ValueKind.Int, Domain = new object?[] { 1L, 2L, 3L } } };
        // "^" is not a translatable arithmetic op → TransInt inner switch throws → post-filter. The
        // C# evaluator coerces the unknown "^" result to 0, so 0 < 5 holds and all candidates
        // survive — the assertion confirms the fallback path executed (results returned, not an error).
        var cons = new List<SolverConstraint>
        {
            new PredicateConstraint { Expr = Bin("<", Bin("^", Var("a"), Int(1)), Int(5)) },
        };
        Assert.Equal(3, new Z3Solver().Generate(ps, cons, new CombinationSpec(), 100).Count);
    }

    [Fact]
    public void Z3_UnconstrainedBool_EnumeratesBothValues()
    {
        var ps = new List<SolverParam> { new() { Name = "flag", Kind = ValueKind.Bool } }; // no domain/In
        var res = new Z3Solver().Generate(ps, new List<SolverConstraint>(), new CombinationSpec(), 100);
        var vals = res.Select(r => Convert.ToBoolean(r["flag"])).OrderBy(x => x).ToList();
        Assert.Equal(new[] { false, true }, vals);
    }
}
