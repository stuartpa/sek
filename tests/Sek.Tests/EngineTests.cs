using System.Linq;
using Sek.Engine;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for <c>Sek.Engine</c>: the action-universe resolver and an end-to-end breadth-first
/// exploration of a small in-test <see cref="ModelProgram"/> (exercises <see cref="Explorer"/>,
/// <see cref="ModelIntrospector"/>, guard handling, accepting conditions, and requirement capture).
/// </summary>
public class EngineTests
{
    // ---- ActionImportResolver ----------------------------------------------------------

    [Fact]
    public void Resolve_ActionAll_ImportsRulesQualifiedByType()
    {
        var all = new[] { "Svc.Open", "Svc.Close", "Other.Ping" };
        var allowed = ActionImportResolver.Resolve(new[] { "Svc" }, System.Array.Empty<string>(), all);
        Assert.Contains("Svc.Open", allowed);
        Assert.Contains("Svc.Close", allowed);
        Assert.DoesNotContain("Other.Ping", allowed);
    }

    [Fact]
    public void Resolve_ExplicitLabel_IsImported()
    {
        var all = new[] { "Svc.Open", "Svc.Close" };
        var allowed = ActionImportResolver.Resolve(System.Array.Empty<string>(), new[] { "Svc.Open" }, all);
        Assert.Contains("Svc.Open", allowed);
        Assert.DoesNotContain("Svc.Close", allowed);
    }

    [Fact]
    public void Resolve_NothingResolves_ImportsEverything()
    {
        var all = new[] { "Inc", "Reset" }; // bare labels, no adapter qualifier
        var allowed = ActionImportResolver.Resolve(new[] { "NoSuchAdapter" }, System.Array.Empty<string>(), all);
        Assert.Equal(all.OrderBy(x => x), allowed.OrderBy(x => x)); // safe default: all
    }

    // ---- End-to-end exploration --------------------------------------------------------

    /// <summary>A tiny counter model: Inc up to 3 (guarded), Reset to 0 (guarded), accepting at 3.</summary>
    public sealed class CounterModel : ModelProgram
    {
        public int N { get; set; }

        [Rule("Counter.Inc")]
        public void Inc()
        {
            Require(N < 3, "at cap");
            Requirement.Capture("REQ-INC");
            N++;
        }

        [Rule("Counter.Reset")]
        public void Reset()
        {
            Require(N > 0, "already zero");
            N = 0;
        }

        [AcceptingCondition]
        public bool AtCap() => N == 3;
    }

    [Fact]
    public void Introspector_Reflects_Rules_And_AcceptingConditions()
    {
        var introspector = new ModelIntrospector(typeof(CounterModel));
        Assert.Equal(new[] { "Counter.Inc", "Counter.Reset" }, introspector.Rules.Select(r => r.ActionLabel));
        Assert.Single(introspector.AcceptingConditions);
    }

    [Fact]
    public void Introspector_Rejects_NonModelProgramType()
    {
        Assert.Throws<System.ArgumentException>(() => new ModelIntrospector(typeof(string)));
    }

    [Fact]
    public void Explore_CounterModel_ProducesExpectedStateSpace()
    {
        var introspector = new ModelIntrospector(typeof(CounterModel));
        var result = new Explorer(introspector).Explore(nameof(CounterModel));

        // Reachable states are N = 0,1,2,3 → 4 states.
        Assert.Equal(4, result.Graph.States.Count);
        // Exactly one accepting state (N == 3).
        Assert.Single(result.Graph.States, s => s.Accepting);
        // Both actions appear as transitions.
        var actions = result.Graph.Transitions.Select(t => t.Action.Name).Distinct().ToList();
        Assert.Contains("Counter.Inc", actions);
        Assert.Contains("Counter.Reset", actions);
        // The Inc rule captured its requirement id during exploration.
        Assert.Contains("REQ-INC", result.CapturedRequirements);
        // A rooted graph with an initial state.
        Assert.NotNull(result.Graph.InitialStateId);
    }

    [Fact]
    public void Explore_RespectsAllowedActionLabels()
    {
        var introspector = new ModelIntrospector(typeof(CounterModel));
        var options = new ExplorationOptions
        {
            AllowedActionLabels = new System.Collections.Generic.HashSet<string> { "Counter.Inc" },
        };
        var result = new Explorer(introspector, options).Explore(nameof(CounterModel));

        // With only Inc allowed, no Reset transitions and the space is the chain 0→1→2→3.
        Assert.All(result.Graph.Transitions, t => Assert.Equal("Counter.Inc", t.Action.Name));
        Assert.Equal(4, result.Graph.States.Count);
    }
}
