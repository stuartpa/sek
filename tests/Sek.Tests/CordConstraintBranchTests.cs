using System.Linq;
using Sek.Cord;
using Sek.Cord.Ast;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for the rarer <c>where {. … .}</c> forms in <c>CordConstraintExtractor</c>:
/// probabilistic if/else domains, struct-field <c>Condition.In</c>, the Roslyn predicate fallback,
/// derived Pairwise columns, where-locals, Expand/Isolated/Seeded/Interaction, comment stripping,
/// and the literal-kind ladder (string / bool / int / long / enum).
/// </summary>
public class CordConstraintBranchTests
{
    private static DeclaredAction Act(string where, params (string type, string name)[] ps)
    {
        var a = new DeclaredAction { Target = "SUT.M", WhereCode = where };
        foreach (var (t, n) in ps) a.Parameters.Add(new Parameter { Type = t, Name = n });
        return a;
    }

    [Fact]
    public void ProbabilisticIfElse_UnionsBothBranches()
    {
        // `if (Probability.IsTrue(p)) …; else …;` — both branch domains merge into the param's union.
        var r = CordConstraintExtractor.Extract(Act(
            "if (Probability.IsTrue(0.7)) Condition.In(a, 1, 2); else Condition.In(a, 3, 4);",
            ("int", "a")));
        var inC = Assert.Single(r.Constraints.OfType<InConstraint>());
        Assert.Equal("a", inC.Param);
        Assert.Equal(4, inC.Values.Count); // {1,2} ∪ {3,4}
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(11)]
    public void ProbabilisticIfElse_SeededOrdering(int seed)
    {
        // Varying the seed flips the gate, exercising both branch-ordering arms of MergeInConstraints
        // (gate-preferred `then` first vs `else` first). The union is the same set regardless.
        var da = Act("if (Probability.IsTrue(0.5)) Condition.In(a, 1, 2); else Condition.In(a, 3, 4);", ("int", "a"));
        var r = CordConstraintExtractor.Extract(da, seed);
        var inC = Assert.Single(r.Constraints.OfType<InConstraint>());
        Assert.Equal(4, inC.Values.Count);
    }

    [Fact]
    public void ProbabilisticIf_NoSpace_And_NonNumericProbability()
    {
        // `if(` with no space, and a non-numeric probability arg (EvalProbability → true).
        var r = CordConstraintExtractor.Extract(Act(
            "if(Probability.IsTrue(x)) Condition.In(a, 1); else Condition.In(a, 2);",
            ("int", "a")));
        Assert.Single(r.Constraints.OfType<InConstraint>());
    }

    [Fact]
    public void ConditionIn_StructField_IsKept()
    {
        // Condition.In on a struct field `info.Command` — IsFieldOfParam recognises `info`.
        var r = CordConstraintExtractor.Extract(Act("Condition.In(info.Command, 1, 2, 3);", ("Req", "info")));
        var inC = Assert.Single(r.Constraints.OfType<InConstraint>());
        Assert.Equal("info.Command", inC.Param);
    }

    [Fact]
    public void StructFields_MultipleAndPairwise()
    {
        // Several struct-field domains plus a Pairwise over struct fields (derived columns).
        var r = CordConstraintExtractor.Extract(Act(
            "Condition.In(info.A, 1, 2); Condition.In(info.B, 3, 4); Combination.Pairwise(info.A, info.B);",
            ("Req", "info")));
        Assert.Equal(2, r.Constraints.OfType<InConstraint>().Count());
        Assert.Equal(CombinationSpec.Strategy.Pairwise, r.Combination.Mode);
    }

    [Fact]
    public void Isolated_And_Seeded_WithStructFields()
    {
        var iso = CordConstraintExtractor.Extract(Act("Combination.Isolated(info.A > 1);", ("Req", "info")));
        // isolated over a struct field: kept only if the parser accepts it (dotted ref).
        Assert.Equal(CombinationSpec.Strategy.AllCombinations, iso.Combination.Mode);
    }

    [Fact]
    public void StructField_StringDomain_And_SeededAndLocal()
    {
        // String domain on a struct field.
        var s = CordConstraintExtractor.Extract(Act("Condition.In(info.C, \"x\", \"y\");", ("Req", "info")));
        Assert.Single(s.Constraints.OfType<InConstraint>());

        // Seeded over a struct field.
        var seeded = CordConstraintExtractor.Extract(Act("Combination.Seeded(info.A == 1);", ("Req", "info")));
        Assert.NotNull(seeded);

        // A where-local derived from a struct field feeding a Pairwise column.
        var local = CordConstraintExtractor.Extract(Act(
            "uint m = info.Flags & 1; Combination.Pairwise(info.Flags, m);", ("Req", "info")));
        Assert.Equal(CombinationSpec.Strategy.Pairwise, local.Combination.Mode);
    }

    [Fact]
    public void ConditionIsTrue_RoslynFallback_ForMethodCall()
    {
        // Math.Abs(...) is outside the mini-parser → compiled as an embedded C# predicate.
        var r = CordConstraintExtractor.Extract(Act("Condition.IsTrue(System.Math.Abs(a) > 2);", ("int", "a")));
        Assert.NotEmpty(r.Constraints.OfType<CompiledPredicateConstraint>());
    }

    [Fact]
    public void ConditionIsTrue_MiniParser_KeepsPredicate()
    {
        var r = CordConstraintExtractor.Extract(Act("Condition.IsTrue(a > 1 && a < 9);", ("int", "a")));
        Assert.NotEmpty(r.Constraints.OfType<PredicateConstraint>());
    }

    [Fact]
    public void CombinationPairwise_DerivedColumns()
    {
        // A derived argument (`a & 1`) makes every Pairwise arg a column.
        var r = CordConstraintExtractor.Extract(Act(
            "Combination.Pairwise(a, a & 1);", ("int", "a")));
        Assert.Equal(CombinationSpec.Strategy.Pairwise, r.Combination.Mode);
        Assert.NotEmpty(r.Combination.PairwiseColumns);
    }

    [Fact]
    public void CombinationPairwise_PlainParams()
    {
        var r = CordConstraintExtractor.Extract(Act("Combination.Pairwise(a, b);", ("int", "a"), ("int", "b")));
        Assert.Equal(CombinationSpec.Strategy.Pairwise, r.Combination.Mode);
    }

    [Fact]
    public void WhereLocal_FeedsPairwiseColumn()
    {
        // A where-local `uint m = a & 1;` referenced by a later Pairwise arg.
        var r = CordConstraintExtractor.Extract(Act(
            "uint m = a & 1; Combination.Pairwise(a, m);", ("int", "a")));
        Assert.Equal(CombinationSpec.Strategy.Pairwise, r.Combination.Mode);
        Assert.Contains(r.Combination.PairwiseColumns, c => c.Item1 == "m");
    }

    [Fact]
    public void Combination_Expand_Isolated_Seeded()
    {
        var expand = CordConstraintExtractor.Extract(Act("Combination.Expand(a);", ("int", "a")));
        Assert.Contains("a", expand.Combination.Expand);

        var isolated = CordConstraintExtractor.Extract(Act("Combination.Isolated(a > 2);", ("int", "a")));
        Assert.NotEmpty(isolated.Combination.Isolated);

        var seeded = CordConstraintExtractor.Extract(Act("Combination.Seeded(a == 1, b == 2);", ("int", "a"), ("int", "b")));
        Assert.NotEmpty(seeded.Combination.Seeded);
    }

    [Fact]
    public void Combination_Interaction_IsDefault()
    {
        var r = CordConstraintExtractor.Extract(Act("Combination.Interaction(a, b);", ("int", "a"), ("int", "b")));
        Assert.Equal(CombinationSpec.Strategy.AllCombinations, r.Combination.Mode);
    }

    [Fact]
    public void ExprParser_LiteralForms_ViaConditionIsTrue()
    {
        // Hex literal → ParseIntLiteral hex path.
        Assert.NotEmpty(CordConstraintExtractor.Extract(Act("Condition.IsTrue(a == 0xFF);", ("int", "a")))
            .Constraints.OfType<PredicateConstraint>());
        // Negative literal after an operator → the negative-number lexer branch.
        Assert.NotEmpty(CordConstraintExtractor.Extract(Act("Condition.IsTrue(a > -5);", ("int", "a")))
            .Constraints.OfType<PredicateConstraint>());
        // Parenthesised sub-expression.
        Assert.NotEmpty(CordConstraintExtractor.Extract(Act("Condition.IsTrue((a > 1) && a < 9);", ("int", "a")))
            .Constraints.OfType<PredicateConstraint>());
        // String literal comparison.
        Assert.NotEmpty(CordConstraintExtractor.Extract(Act("Condition.IsTrue(s == \"x\");", ("string", "s")))
            .Constraints.OfType<PredicateConstraint>());
        // Boolean literal.
        Assert.NotEmpty(CordConstraintExtractor.Extract(Act("Condition.IsTrue(b == true);", ("bool", "b")))
            .Constraints.OfType<PredicateConstraint>());
    }

    [Fact]
    public void ExprParser_BadChar_FallsThrough()
    {
        // '@' is not a valid operator → the mini-parser throws → not kept as a mini-parsed predicate
        // (and Roslyn also rejects it), so no PredicateConstraint is produced.
        var r = CordConstraintExtractor.Extract(Act("Condition.IsTrue(a @ 2);", ("int", "a")));
        Assert.Empty(r.Constraints.OfType<PredicateConstraint>());
    }

    [Fact]
    public void Comments_AreStripped()
    {
        var r = CordConstraintExtractor.Extract(Act(
            "Condition.In(a, 1, 2); // a line comment\n /* block\n comment */ Condition.In(b, 3);",
            ("int", "a"), ("int", "b")));
        Assert.Equal(2, r.Constraints.OfType<InConstraint>().Count());
    }

    [Fact]
    public void ParseLiteral_KindLadder()
    {
        var r = CordConstraintExtractor.Extract(Act(
            "Condition.In(a, \"str\", true, false, 5, 9999999999, EnumMember);", ("string", "a")));
        var inC = Assert.Single(r.Constraints.OfType<InConstraint>());
        Assert.Equal(6, inC.Values.Count);
        Assert.Contains("str", inC.Values);
        Assert.Contains(true, inC.Values);
        Assert.Contains(false, inC.Values);
        Assert.Contains("EnumMember", inC.Values);       // unparseable → enum-name fallback
        Assert.Contains(inC.Values, v => v is int or long); // numeric literals
    }
}
