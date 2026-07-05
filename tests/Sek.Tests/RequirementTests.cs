using Sek.Engine;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Item 3 — requirement coverage. Covers the <see cref="Requirement"/> capture buffer and the
/// engine's aggregation of captured requirement ids across an exploration.
/// </summary>
public class RequirementTests
{
    [Fact]
    public void Capture_AccumulatesUntilReset()
    {
        Requirement.Reset();
        Assert.Empty(Requirement.Captured);
        Requirement.Capture("R1");
        Requirement.Capture("R2");
        Assert.Equal(new[] { "R1", "R2" }, Requirement.Captured);
        Requirement.Reset();
        Assert.Empty(Requirement.Captured);
    }

    [Fact]
    public void Capture_IgnoresBlankIds()
    {
        Requirement.Reset();
        Requirement.Capture("");
        Requirement.Capture("   ");
        Assert.Empty(Requirement.Captured);
    }

    [Fact]
    public void Explorer_AggregatesCapturedRequirements()
    {
        var introspector = new ModelIntrospector(typeof(Gate));
        var result = new Explorer(introspector, new ExplorationOptions { MaxDepth = 50 }).Explore("Gate");

        // The model captures REQ_OPEN once opened, and REQ_FULL when it reaches 2.
        Assert.Contains("REQ_OPEN", result.CapturedRequirements);
        Assert.Contains("REQ_FULL", result.CapturedRequirements);
        Assert.Equal(new[] { "REQ_FULL", "REQ_OPEN" }, result.CapturedRequirements.ToArray()); // sorted
        Assert.Equal("REQ_FULL,REQ_OPEN", result.Graph.Metadata["requirementsCaptured"]);
    }

    [Fact]
    public void Explorer_NoCaptures_NoRequirementsMetadata()
    {
        var introspector = new ModelIntrospector(typeof(Silent));
        var result = new Explorer(introspector, new ExplorationOptions { MaxDepth = 50 }).Explore("Silent");
        Assert.Empty(result.CapturedRequirements);
        Assert.False(result.Graph.Metadata.ContainsKey("requirementsCaptured"));
    }

    /// <summary>Opens a gate then counts to 2, capturing requirements along the way.</summary>
    public sealed class Gate : ModelProgram
    {
        public bool Open { get; set; }
        public int N { get; set; }

        [Rule("OpenGate")]
        public void OpenGate()
        {
            Require(!Open, "already open");
            Open = true;
            Requirement.Capture("REQ_OPEN");
        }

        [Rule("Step")]
        public void Step()
        {
            Require(Open && N < 2, "closed or full");
            N++;
            if (N == 2) Requirement.Capture("REQ_FULL");
        }
    }

    /// <summary>A model with no requirement captures.</summary>
    public sealed class Silent : ModelProgram
    {
        public int N { get; set; }

        [Rule("Inc")]
        public void Inc()
        {
            Require(N < 2, "max");
            N++;
        }
    }
}
