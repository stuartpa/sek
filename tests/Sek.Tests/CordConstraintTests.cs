using System.Linq;
using Sek.Cord;
using Sek.Cord.Ast;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// High-yield coverage for <c>Sek.Cord.CordConstraintExtractor</c> (the <c>where {. … .}</c> →
/// solver-constraint translator) and, transitively, the embedded-expression parser. Exercises
/// Condition.In (params + struct fields), Condition.IsTrue (mini-parser + Roslyn fallback),
/// Combination.* (Pairwise/derived-columns/Expand/Isolated/Seeded/Interaction), where-locals,
/// comment stripping, and the probabilistic union.
/// </summary>
public class CordConstraintTests
{
    private static DeclaredAction Act(string where, params (string type, string name)[] ps)
    {
        var a = new DeclaredAction { Target = "SUT.M", WhereCode = where };
        foreach (var (t, n) in ps) a.Parameters.Add(new Parameter { Type = t, Name = n });
        return a;
    }

    [Fact]
    public void Empty_WhereCode_YieldsNoConstraints()
    {
        var r = CordConstraintExtractor.Extract(Act("", ("int", "a")));
        Assert.Empty(r.Constraints);
        Assert.Equal(CombinationSpec.Strategy.AllCombinations, r.Combination.Mode);
    }

    [Fact]
    public void ConditionIn_Param_BecomesInConstraint()
    {
        var r = CordConstraintExtractor.Extract(Act("Condition.In(a, 1, 2, 3);", ("int", "a")));
        var inC = Assert.Single(r.Constraints.OfType<InConstraint>());
        Assert.Equal("a", inC.Param);
        Assert.Equal(3, inC.Values.Count);
    }

    [Fact]
    public void ConditionIn_StructField_IsKept()
    {
        var r = CordConstraintExtractor.Extract(Act("Condition.In(info.Name, \"x\", \"y\");", ("JobInfo", "info")));
        var inC = Assert.Single(r.Constraints.OfType<InConstraint>());
        Assert.Equal("info.Name", inC.Param);
    }

    [Fact]
    public void ConditionIn_UnknownParam_IsIgnored()
    {
        var r = CordConstraintExtractor.Extract(Act("Condition.In(nope, 1, 2);", ("int", "a")));
        Assert.Empty(r.Constraints);
    }

    [Fact]
    public void ConditionIsTrue_MiniParserExpression_BecomesPredicate()
    {
        var r = CordConstraintExtractor.Extract(Act("Condition.IsTrue(priority >= 0 & priority < 3);", ("int", "priority")));
        Assert.Single(r.Constraints.OfType<PredicateConstraint>());
    }

    [Fact]
    public void ConditionIsTrue_RoslynFallback_BecomesCompiledPredicate()
    {
        // method call -> outside the mini-parser grammar -> Roslyn-compiled post-filter
        var r = CordConstraintExtractor.Extract(Act("Condition.IsTrue(name.StartsWith(\"t\"));", ("string", "name")));
        Assert.Single(r.Constraints.OfType<CompiledPredicateConstraint>());
    }

    [Fact]
    public void ConditionIsTrue_NoParamReference_IsDropped()
    {
        var r = CordConstraintExtractor.Extract(Act("Condition.IsTrue(1 < 2);", ("int", "a")));
        // references no parameter and is a constant → not kept as a parameter predicate
        Assert.Empty(r.Constraints.OfType<CompiledPredicateConstraint>());
    }

    [Fact]
    public void Combination_Pairwise_PlainParams_SetsPairwiseMode_NoColumns()
    {
        var r = CordConstraintExtractor.Extract(Act("Combination.Pairwise(a, b);", ("int", "a"), ("int", "b")));
        Assert.Equal(CombinationSpec.Strategy.Pairwise, r.Combination.Mode);
        Assert.Empty(r.Combination.PairwiseColumns);
    }

    [Fact]
    public void Combination_Pairwise_DerivedColumns_FromLocalsAndCompound()
    {
        var where =
            "uint mon = days & 0x1;\n" +
            "Combination.Pairwise(days, mon, time & 0x2);";
        var r = CordConstraintExtractor.Extract(Act(where, ("int", "days"), ("int", "time")));
        Assert.Equal(CombinationSpec.Strategy.Pairwise, r.Combination.Mode);
        Assert.NotEmpty(r.Combination.PairwiseColumns);
    }

    [Fact]
    public void Combination_Expand_CollectsListedParams()
    {
        var r = CordConstraintExtractor.Extract(Act("Combination.Expand(a, b);", ("int", "a"), ("int", "b")));
        Assert.Equal(new[] { "a", "b" }, r.Combination.Expand.OrderBy(x => x));
    }

    [Fact]
    public void Combination_Isolated_AddsPredicate()
    {
        var r = CordConstraintExtractor.Extract(Act("Combination.Isolated(a == 1);", ("int", "a")));
        Assert.Single(r.Combination.Isolated);
    }

    [Fact]
    public void Combination_Seeded_AddsConjunction()
    {
        var r = CordConstraintExtractor.Extract(Act("Combination.Seeded(a == 1, b == 2);", ("int", "a"), ("int", "b")));
        var conj = Assert.Single(r.Combination.Seeded);
        Assert.Equal(2, conj.Count);
    }

    [Fact]
    public void Comments_AreStripped_SoFollowingStatementSurvives()
    {
        var where =
            "// choose the name domain\n" +
            "Condition.In(name, \"a\", \"b\");";
        var r = CordConstraintExtractor.Extract(Act(where, ("string", "name")));
        Assert.Single(r.Constraints.OfType<InConstraint>());
    }

    [Fact]
    public void Probability_IfElse_UnionsBothBranchValues()
    {
        var where =
            "if (Probability.IsTrue(0.8))\n" +
            "    Condition.In(name, \"foo\", \"bar\");\n" +
            "else\n" +
            "    Condition.In(name, \"baz\");";
        var r = CordConstraintExtractor.Extract(Act(where, ("string", "name")), randomSeed: 1);
        var inC = Assert.Single(r.Constraints.OfType<InConstraint>()); // merged
        Assert.Equal(3, inC.Values.Count); // foo, bar, baz
    }

    [Fact]
    public void KindOf_HonoursParameterTypes_ForRoslynCompilation()
    {
        // long + bool params exercised through a Roslyn-compiled predicate
        var r = CordConstraintExtractor.Extract(Act("Condition.IsTrue(System.Math.Abs(n) > 0 && flag);", ("long", "n"), ("bool", "flag")));
        Assert.Single(r.Constraints.OfType<CompiledPredicateConstraint>());
    }
}
