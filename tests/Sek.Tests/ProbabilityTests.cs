using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Solver;
using SpecExplorerKit.Components.Random;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Item 1 — <c>Probability.IsTrue(p)</c> seeded sampling. Verifies the gate is reproducible,
/// respects the probability, and that a Cord <c>where</c> block selects its domain branch via
/// the seed rather than a fixed majority rule.
/// </summary>
public class ProbabilityTests
{
    [Fact]
    public void Gate_Boundaries_AreDeterministic()
    {
        var g = new ProbabilityGate(1);
        Assert.True(g.IsTrue(1.0));   // certain
        Assert.False(g.IsTrue(0.0));  // impossible
        Assert.True(g.IsTrue(1.5));   // clamps above 1
        Assert.False(g.IsTrue(-0.2)); // clamps below 0
    }

    [Fact]
    public void Gate_SameSeed_SameSequence()
    {
        var a = new ProbabilityGate(42);
        var b = new ProbabilityGate(42);
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(a.IsTrue(0.5), b.IsTrue(0.5));
        }
    }

    [Fact]
    public void Gate_RespectsProbability_OverManyDraws()
    {
        var g = new ProbabilityGate(12345);
        var trues = 0;
        const int n = 20000;
        for (var i = 0; i < n; i++)
        {
            if (g.IsTrue(0.8)) trues++;
        }

        var ratio = trues / (double)n;
        Assert.InRange(ratio, 0.78, 0.82); // ~80%
    }

    [Fact]
    public void Gate_DifferentSeeds_CanDiffer()
    {
        // There must exist seeds for which the first IsTrue(0.8) draw differs, otherwise the
        // gate would be a constant (i.e. the old majority-branch bug).
        var anyTrue = false;
        var anyFalse = false;
        for (var seed = 0; seed < 40; seed++)
        {
            if (new ProbabilityGate(seed).IsTrue(0.8)) anyTrue = true;
            else anyFalse = true;
        }

        Assert.True(anyTrue);
        Assert.True(anyFalse);
    }

    private static DeclaredAction ProbabilityAction() => new()
    {
        Target = "CreateFile",
        WhereCode =
            "if (Probability.IsTrue(0.8))\n" +
            "    Condition.In(name, \"foo\", \"bar\");\n" +
            "else\n" +
            "    Condition.In(name, \"@^@\\\\\");\n",
        Parameters = { new Parameter { Type = "string", Name = "name" } },
    };

    [Fact]
    public void Extract_Probability_IsReproducible_ForASeed()
    {
        var a = CordConstraintExtractor.Extract(ProbabilityAction(), randomSeed: 2);
        var b = CordConstraintExtractor.Extract(ProbabilityAction(), randomSeed: 2);
        var da = a.Constraints.OfType<InConstraint>().Single().Values;
        var db = b.Constraints.OfType<InConstraint>().Single().Values;
        Assert.Equal(db, da);
    }

    [Fact]
    public void Extract_Probability_UnionsBothBranches()
    {
        // Both branches are reachable, so the parameter domain is their union (no missed states).
        var vals = CordConstraintExtractor.Extract(ProbabilityAction(), randomSeed: 2)
            .Constraints.OfType<InConstraint>().Single().Values.Select(v => v?.ToString()).ToList();
        Assert.Equal(3, vals.Count);                       // foo, bar (then) + @^@ (else)
        Assert.Contains("foo", vals);
        Assert.Contains("bar", vals);
        Assert.Contains(vals, v => v is not null && v.StartsWith("@^@")); // else-branch value
    }

    [Fact]
    public void Extract_Probability_SeedControlsBranchOrder()
    {
        // The seeded gate orders the union so the more-likely branch's values come first; some
        // seeds put the `then` (foo/bar) branch first, others the `else` (@^@) branch first.
        string? thenFirst = null, elseFirst = null;
        for (var seed = 0; seed < 60 && (thenFirst is null || elseFirst is null); seed++)
        {
            var first = CordConstraintExtractor.Extract(ProbabilityAction(), seed)
                .Constraints.OfType<InConstraint>().Single().Values.First()?.ToString();
            if (first is "foo" or "bar") thenFirst = $"seed {seed}";
            else elseFirst = $"seed {seed}";
        }

        Assert.NotNull(thenFirst);
        Assert.NotNull(elseFirst);
    }
}
