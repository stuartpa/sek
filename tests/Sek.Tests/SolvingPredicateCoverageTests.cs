using System;
using System.Collections.Generic;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for <see cref="PredicateEval"/>: the logical-vs-bitwise <c>&amp;</c>/<c>|</c>
/// operators, every comparison/arithmetic operator, division/modulo by zero, unary operators, the
/// equality type ladder (null / string / bool / long / enum), and unknown-operator fallbacks.
/// </summary>
public class SolvingPredicateCoverageTests
{
    private static readonly IReadOnlyDictionary<string, object?> Empty = new Dictionary<string, object?>();

    private static Expr Lit(object? v) => new LitExpr { Value = v };
    private static Expr Var(string n) => new VarExpr { Name = n };
    private static Expr Bin(string op, Expr l, Expr r) => new BinExpr { Op = op, Left = l, Right = r };
    private static Expr Un(string op, Expr o) => new UnExpr { Op = op, Operand = o };

    private static object? Ev(Expr e, IReadOnlyDictionary<string, object?>? a = null) => PredicateEval.Evaluate(e, a ?? Empty);

    [Fact]
    public void Logical_Vs_Bitwise_AndOr()
    {
        // `&`/`|` with bool operands → logical; with numeric operands → bitwise.
        Assert.Equal(false, Ev(Bin("&", Lit(true), Lit(false))));
        Assert.Equal(true, Ev(Bin("&&", Lit(true), Lit(true))));
        Assert.Equal(2L, Ev(Bin("&", Lit(6L), Lit(2L))));
        Assert.Equal(true, Ev(Bin("|", Lit(true), Lit(false))));
        Assert.Equal(true, Ev(Bin("||", Lit(false), Lit(true))));
        Assert.Equal(5L, Ev(Bin("|", Lit(4L), Lit(1L))));
    }

    [Fact]
    public void Comparisons_And_Arithmetic()
    {
        Assert.Equal(true, Ev(Bin("<", Lit(1L), Lit(2L))));
        Assert.Equal(true, Ev(Bin("<=", Lit(2L), Lit(2L))));
        Assert.Equal(true, Ev(Bin(">", Lit(3L), Lit(2L))));
        Assert.Equal(true, Ev(Bin(">=", Lit(2L), Lit(2L))));
        Assert.Equal(3L, Ev(Bin("+", Lit(1L), Lit(2L))));
        Assert.Equal(1L, Ev(Bin("-", Lit(3L), Lit(2L))));
        Assert.Equal(6L, Ev(Bin("*", Lit(2L), Lit(3L))));
        Assert.Equal(3L, Ev(Bin("/", Lit(6L), Lit(2L))));
        Assert.Equal(0L, Ev(Bin("/", Lit(6L), Lit(0L))));  // divide by zero → 0
        Assert.Equal(2L, Ev(Bin("%", Lit(8L), Lit(3L))));
        Assert.Equal(0L, Ev(Bin("%", Lit(8L), Lit(0L))));  // mod by zero → 0
    }

    [Fact]
    public void Equality_TypeLadder()
    {
        Assert.Equal(true, Ev(Bin("==", Lit(null), Lit(null))));   // null branch
        Assert.Equal(false, Ev(Bin("==", Lit(null), Lit(1L))));
        Assert.Equal(true, Ev(Bin("==", Lit("a"), Lit("a"))));     // string branch
        Assert.Equal(false, Ev(Bin("!=", Lit("a"), Lit("a"))));
        Assert.Equal(true, Ev(Bin("==", Lit(true), Lit(true))));   // bool branch
        Assert.Equal(true, Ev(Bin("==", Lit(3L), Lit(3L))));       // long branch
        Assert.Equal(true, Ev(Bin("==", Lit(DayOfWeek.Monday), Lit(1L)))); // enum → long
    }

    [Fact]
    public void Unary_And_Unknown_Operators()
    {
        Assert.Equal(true, Ev(Un("!", Lit(false))));
        Assert.Equal(-5L, Ev(Un("-", Lit(5L))));
        Assert.Null(Ev(Un("?", Lit(1L))));            // unknown unary → null
        Assert.Null(Ev(Bin("^^", Lit(1L), Lit(2L)))); // unknown binary → null
    }

    [Fact]
    public void Vars_And_Eval_ReturnsBool()
    {
        var a = new Dictionary<string, object?> { ["x"] = 5L };
        Assert.Equal(5L, Ev(Var("x"), a));
        Assert.Null(Ev(Var("missing"), a));

        Assert.True(PredicateEval.Eval(Bin("==", Var("x"), Lit(5L)), a));
        Assert.False(PredicateEval.Eval(Var("x"), a)); // non-bool value → Eval false
    }
}
