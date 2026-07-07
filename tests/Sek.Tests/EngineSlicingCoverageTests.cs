using System.Collections.Generic;
using System.Linq;
using Sek.Cord.Ast;
using Sek.Engine;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for <see cref="BehaviorExplorer.CompiledScenario"/>'s runtime slicing-step
/// methods (used when a scenario slices a model program): argument-pinned matching, the <c>_</c>
/// wildcard, positional/length mismatches, return-binding capture and env matching, and the
/// bare-label fallback.
/// </summary>
public class EngineSlicingCoverageTests
{
    private static BehaviorExplorer Explorer() =>
        new(_ => null, new[] { "A", "B" });

    private static InvocationBehavior Inv(string target, params string[] args) =>
        new() { Target = target, Args = args.Length == 0 ? null : args.ToList() };

    [Fact]
    public void PinnedArgs_Match_Mismatch_LengthMismatch_Wildcard()
    {
        // Scenario: A(1) | A(_)  → the DFA has pinned transitions "A(1)" and "A(_)".
        var body = new ChoiceBehavior { Items = { Inv("A", "1"), Inv("A", "_") } };
        var sc = Explorer().Compile(body);
        var start = sc.Start;

        // Permits sees the pinned form via the "A(" prefix.
        Assert.True(sc.Permits(start, "A"));
        Assert.False(sc.Permits(start, "Z"));

        // Exact pinned match.
        Assert.True(sc.TryStepArgs(start, "A", new[] { "1" }, out _));
        // Wildcard "_" matches any single arg.
        Assert.True(sc.TryStepArgs(start, "A", new[] { "999" }, out _));
        // Value mismatch (neither "1" nor wildcard-length differs) — "2" still matches "_".
        Assert.True(sc.TryStepArgs(start, "A", new[] { "2" }, out _));
        // Arity mismatch → no match.
        Assert.False(sc.TryStepArgs(start, "A", new[] { "1", "2" }, out _));
        // Unknown label → no match.
        Assert.False(sc.TryStepArgs(start, "Z", new[] { "1" }, out _));

        // ArgPatterns enumerates the pinned patterns from this state.
        var patterns = sc.ArgPatterns(start, "A").ToList();
        Assert.NotEmpty(patterns);
    }

    [Fact]
    public void PlainTryStep_And_Permits_BareLabel()
    {
        var sc = Explorer().Compile(new SequenceBehavior { Items = { Inv("A"), Inv("B") } });
        Assert.True(sc.Permits(sc.Start, "A"));      // bare label present
        Assert.True(sc.TryStep(sc.Start, "A", out var s1));
        Assert.True(sc.TryStep(s1, "B", out _));
        Assert.False(sc.TryStep(sc.Start, "B", out _)); // not enabled yet
        Assert.False(sc.TryStep(-1, "A", out _));       // invalid state
    }

    [Fact]
    public void ReturnBinding_Capture_And_EnvMatch()
    {
        // A / v ; B(v) — A produces value bound to v; B consumes it.
        var producer = new InvocationBehavior { Target = "A", ReturnBinding = "v" };
        var consumer = new InvocationBehavior { Target = "B", Args = new List<string> { "v" } };
        var sc = Explorer().Compile(new SequenceBehavior { Items = { producer, consumer } });
        Assert.True(sc.HasReturnBindings);

        // Step the producer (bare label) — reports the bound variable to capture.
        Assert.True(sc.TryStepBinding(sc.Start, "A", System.Array.Empty<string>(), new Dictionary<string, string>(), out var s1, out var boundVar));
        Assert.Equal("v", boundVar);

        // Consume with the captured value → matches; a different value → no match.
        var env = new Dictionary<string, string> { ["v"] = "5" };
        Assert.True(sc.TryStepBinding(s1, "B", new[] { "5" }, env, out _, out _));
        Assert.False(sc.TryStepBinding(s1, "B", new[] { "6" }, env, out _, out _));

        // Unbound var acts as a wildcard.
        Assert.True(sc.TryStepBinding(s1, "B", new[] { "anything" }, new Dictionary<string, string>(), out _, out _));

        // Invalid state.
        Assert.False(sc.TryStepBinding(-1, "A", System.Array.Empty<string>(), new Dictionary<string, string>(), out _, out _));
    }
}
