using Sek.Engine;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Item 5 — <c>action all &lt;Adapter&gt;</c> resolution. Covers the pure
/// <see cref="ActionImportResolver"/> and the engine's action-universe restriction.
/// </summary>
public class ActionImportTests
{
    private static readonly string[] All = { "AdapterA.Foo", "AdapterA.Baz", "AdapterB.Bar" };

    [Fact]
    public void ActionAll_ImportsAllRulesOfNamedAdapter()
    {
        var allowed = ActionImportResolver.Resolve(new[] { "AdapterA" }, System.Array.Empty<string>(), All);
        Assert.Equal(new[] { "AdapterA.Baz", "AdapterA.Foo" }, allowed.OrderBy(x => x));
    }

    [Fact]
    public void ActionAll_QualifiedAdapterType_MatchesByLeafSegment()
    {
        // `action all Chat.Adapters.AdapterB` should still match `AdapterB.Bar`.
        var allowed = ActionImportResolver.Resolve(new[] { "Chat.Adapters.AdapterB" }, System.Array.Empty<string>(), All);
        Assert.Equal(new[] { "AdapterB.Bar" }, allowed.ToArray());
    }

    [Fact]
    public void Explicit_ImportsNamedAction_ByFullOrLeafName()
    {
        var byFull = ActionImportResolver.Resolve(System.Array.Empty<string>(), new[] { "AdapterA.Foo" }, All);
        Assert.Equal(new[] { "AdapterA.Foo" }, byFull.ToArray());
    }

    [Fact]
    public void BareLabels_NoAdapterMatch_FallsBackToAll()
    {
        var bare = new[] { "StartServer", "LogonRequest" };
        var allowed = ActionImportResolver.Resolve(new[] { "ChatSetupAdapter", "ChatAdapter" }, System.Array.Empty<string>(), bare);
        Assert.Equal(bare.OrderBy(x => x), allowed.OrderBy(x => x)); // nothing matched -> import all
    }

    [Fact]
    public void NoDeclarations_ImportsEverything()
    {
        var allowed = ActionImportResolver.Resolve(System.Array.Empty<string>(), System.Array.Empty<string>(), All);
        Assert.Equal(All.OrderBy(x => x), allowed.OrderBy(x => x));
    }

    [Fact]
    public void Explorer_RestrictsToAllowedActions()
    {
        var introspector = new ModelIntrospector(typeof(TwoAdapters));
        // Only import AdapterA: AdapterB.Off must never fire, so the gate can never re-close.
        var opts = new ExplorationOptions { MaxDepth = 20, MaxStates = 50 };
        opts.AllowedActionLabels = ActionImportResolver.Resolve(
            new[] { "AdapterA" }, System.Array.Empty<string>(),
            introspector.Rules.Select(r => r.ActionLabel));

        var result = new Explorer(introspector, opts).Explore("TwoAdapters");

        var labels = result.Graph.Transitions.Select(t => t.Action.Name).Distinct().ToList();
        Assert.Contains("AdapterA.On", labels);
        Assert.DoesNotContain("AdapterB.Off", labels); // filtered out
        Assert.Equal(2, result.Graph.States.Count); // Off, On — cannot return to Off
    }

    /// <summary>Two "adapters": AdapterA turns a switch on, AdapterB turns it off.</summary>
    public sealed class TwoAdapters : ModelProgram
    {
        public bool On { get; set; }

        [Rule("AdapterA.On")]
        public void On_() { Require(!On, "already on"); On = true; }

        [Rule("AdapterB.Off")]
        public void Off_() { Require(On, "already off"); On = false; }
    }
}
