using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Engine;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Items 6 &amp; 7 — action kinds (call/return/event) and return-bindings. Covers parsing the
/// kind and <c>/ var</c> binding, the engine tagging event transitions and capturing return
/// values, and the return-binding dataflow resolver.
/// </summary>
public class ActionSemanticsTests
{
    // ---- Item 6: action kinds ----

    [Fact]
    public void Parser_RetainsEventKind()
    {
        var doc = CordDocument.ParseText(
            "config C { action event void Pub.Received(string data); action void Pub.Send(string data); }");
        var cfg = doc.GetConfiguration("C")!;
        var received = cfg.DeclaredActions.Single(a => a.Target == "Pub.Received");
        var send = cfg.DeclaredActions.Single(a => a.Target == "Pub.Send");
        Assert.Equal(ActionKind.Event, received.Kind);
        Assert.Equal(ActionKind.Call, send.Kind);
    }

    [Fact]
    public void CordDocument_ResolvesEventActionsForMachine()
    {
        var doc = CordDocument.ParseText(
            "config C { action event void Pub.Received(string data); action void Pub.Send(string data); }\n" +
            "machine M() : C { Pub.Send }");
        var events = doc.ResolveMachineEventActions("M");
        Assert.Contains("Received", events);
        Assert.DoesNotContain("Send", events);
    }

    [Fact]
    public void Explorer_TagsEventTransitions()
    {
        var introspector = new ModelIntrospector(typeof(Emitter));
        var opts = new ExplorationOptions { MaxDepth = 10 };
        opts.EventActionLabels = new HashSet<string> { "Ring" };
        var result = new Explorer(introspector, opts).Explore("Emitter");

        var ring = result.Graph.Transitions.First(t => t.Action.Name == "Ring");
        var press = result.Graph.Transitions.First(t => t.Action.Name == "Press");
        Assert.True(ring.Action.IsEvent);      // event
        Assert.False(press.Action.IsEvent);    // controllable call
    }

    // ---- Item 7: return-bindings ----

    [Fact]
    public void Parser_RetainsReturnBinding()
    {
        var doc = CordDocument.ParseText(
            "config C { action int Svc.Open(); action void Svc.Use(int h); }\n" +
            "machine M() : C { Svc.Open()/handle ; Svc.Use(handle) }");
        var seq = (SequenceBehavior)doc.GetMachine("M")!.Body!;
        var open = (InvocationBehavior)seq.Items[0];
        Assert.Equal("handle", open.ReturnBinding);
    }

    [Fact]
    public void Explorer_CapturesReturnValue()
    {
        var introspector = new ModelIntrospector(typeof(Counter));
        var result = new Explorer(introspector, new ExplorationOptions { MaxDepth = 10 }).Explore("Counter");
        // Next() returns the new count; the first transition must record result "1".
        var first = result.Graph.Transitions.First(t => t.Action.Name == "Next");
        Assert.Equal("1", first.Action.Result);
    }

    [Fact]
    public void ReturnBindingResolver_SubstitutesCapturedValueDownstream()
    {
        var trace = new[]
        {
            new ReturnBindingResolver.Step("Open", "handle", System.Array.Empty<string>(), "7"),
            new ReturnBindingResolver.Step("Use", null, new[] { "handle" }, null),
        };
        var resolved = ReturnBindingResolver.ResolveArguments(trace);
        Assert.Empty(resolved[0]);                 // Open has no args
        Assert.Equal(new[] { "7" }, resolved[1]);  // Use(handle) -> Use(7)
    }

    [Fact]
    public void ReturnBindingResolver_UnboundReferenceIsUnchanged_AndSelfNotVisible()
    {
        var trace = new[]
        {
            // 'x' references its own not-yet-bound binding -> stays "x"; later step sees it.
            new ReturnBindingResolver.Step("A", "x", new[] { "x" }, "5"),
            new ReturnBindingResolver.Step("B", null, new[] { "x", "y" }, null),
        };
        var resolved = ReturnBindingResolver.ResolveArguments(trace);
        Assert.Equal(new[] { "x" }, resolved[0]);        // its own binding not visible to itself
        Assert.Equal(new[] { "5", "y" }, resolved[1]);   // x bound to 5; y unbound stays
    }

    /// <summary>Emits a controllable Press and an observable Ring.</summary>
    public sealed class Emitter : ModelProgram
    {
        public bool Pressed { get; set; }

        [Rule("Press")]
        public void Press() { Require(!Pressed, "pressed"); Pressed = true; }

        [Rule("Ring")]
        public void Ring() { Require(Pressed, "not pressed"); Pressed = false; }
    }

    /// <summary>A rule that returns its new count value (exercises return capture).</summary>
    public sealed class Counter : ModelProgram
    {
        public int N { get; set; }

        [Rule("Next")]
        public int Next() { Require(N < 2, "max"); N++; return N; }
    }
}
