using System;
using System.Collections.Generic;
using System.Linq;
using Sek.Cli;
using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Engine;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for the <c>sek</c> requirement-coverage steering construct
/// (<see cref="SekCli.InterpretConstruct"/> with <see cref="ConstructKind.RequirementCoverage"/>):
/// the captured-requirements path with RequirementsToCover / MinimumRequirementCount, and the
/// no-capture fallback to the covered action set.
/// </summary>
public class SekSteeringCoverageTests
{
    public sealed class ReqModel : ModelProgram
    {
        public int Step { get; set; }

        [Rule("ReqModel.A")]
        public void A() { Requirement.Capture("R1"); Step++; }

        [Rule("ReqModel.B")]
        public void B() { Requirement.Capture("R2"); Step++; }

        [AcceptingCondition]
        public bool Done() => Step >= 1;
    }

    public sealed class PlainModel : ModelProgram
    {
        public int Step { get; set; }

        [Rule("PlainModel.X")]
        public void X() { Step++; }

        [AcceptingCondition]
        public bool Done() => Step >= 1;
    }

    public sealed class GoalM : ModelProgram
    {
        public bool AtGoal { get; set; }

        [Rule("GoalM.Go")]
        public void Go() { AtGoal = true; }

        [AcceptingCondition]
        public bool Done() => true;
    }

    private static ExplorationResult RunRequirementCoverage(Type modelType, string modelName, IDictionary<string, string>? extraParams = null)
    {
        var intro = new ModelIntrospector(modelType);
        var cord = CordDocument.ParseText(
            $"config C {{ action all {modelName}; }}\nmachine M() : C {{ construct model program from C }}\n");
        var cb = new ConstructBehavior { Kind = ConstructKind.RequirementCoverage, Reference = "C" };
        if (extraParams is not null)
        {
            foreach (var kv in extraParams) cb.Params[kv.Key] = kv.Value;
        }

        var options = new ExplorationOptions { MaxStates = 16, MaxTransitions = 16, MaxDepth = 8 };
        var binds = new Dictionary<string, List<List<string>>>(StringComparer.Ordinal);
        return SekCli.InterpretConstruct(intro, cord, "M", cb, options, "enum", binds);
    }

    [Fact]
    public void RequirementCoverage_WithToCover_And_MinimumCount()
    {
        var r = RunRequirementCoverage(typeof(ReqModel), "ReqModel", new Dictionary<string, string>
        {
            ["RequirementsToCover"] = "R1, R2, R3",
            ["MinimumRequirementCount"] = "2",
        });

        Assert.True(r.Graph.Metadata.ContainsKey("requirementsCovered"));
        Assert.Equal("R1, R2, R3", r.Graph.Metadata["requirementsToCover"]);
        Assert.Contains("R3", r.Graph.Metadata["requirementsMissing"]);          // R3 never captured
        Assert.Equal("False", r.Graph.Metadata["requirementsCoverageComplete"]); // R3 missing
        Assert.Equal("True", r.Graph.Metadata["minimumRequirementCountMet"]);    // 2 of {R1,R2,R3} met >= 2
    }

    [Fact]
    public void RequirementCoverage_CapturedOnly_NoToCover()
    {
        var r = RunRequirementCoverage(typeof(ReqModel), "ReqModel");
        Assert.True(r.Graph.Metadata.ContainsKey("requirementsCovered"));
        Assert.True(r.Graph.Metadata.ContainsKey("requirementCount"));
        Assert.False(r.Graph.Metadata.ContainsKey("requirementsToCover"));
    }

    [Fact]
    public void RequirementCoverage_MinimumCount_NoToCover_UsesCapturedBasis()
    {
        // MinimumRequirementCount without RequirementsToCover → basis is the captured count.
        var r = RunRequirementCoverage(typeof(ReqModel), "ReqModel", new Dictionary<string, string>
        {
            ["MinimumRequirementCount"] = "1",
        });
        Assert.Equal("1", r.Graph.Metadata["minimumRequirementCount"]);
        Assert.Equal("True", r.Graph.Metadata["minimumRequirementCountMet"]);
        Assert.False(r.Graph.Metadata.ContainsKey("requirementsToCover"));
    }

    [Fact]
    public void RequirementCoverage_NoCapture_FallsBackToActionSet()
    {
        var r = RunRequirementCoverage(typeof(PlainModel), "PlainModel");
        // No Requirement.Capture calls → falls back to reporting the covered action set.
        Assert.True(r.Graph.Metadata.ContainsKey("requirements"));
        Assert.True(r.Graph.Metadata.ContainsKey("requirementCount"));
    }

    [Fact]
    public void PointShoot_Degenerate_SteersToGoal()
    {
        // A point-shoot construct with a `with (. AtGoal .)` goal predicate but no Shoot/Completer
        // machines → the degenerate branch prunes the point graph to goal-reaching states.
        var intro = new ModelIntrospector(typeof(GoalM));
        var cord = CordDocument.ParseText(
            "config C { action all GoalM; }\nmachine M() : C { construct model program from C }\n");
        var cb = new ConstructBehavior { Kind = ConstructKind.PointShoot, Reference = "C" };
        cb.Params["with"] = "AtGoal";

        var options = new ExplorationOptions { MaxStates = 16, MaxTransitions = 16, MaxDepth = 8 };
        var binds = new Dictionary<string, List<List<string>>>(StringComparer.Ordinal);
        var r = SekCli.InterpretConstruct(intro, cord, "M", cb, options, "enum", binds);

        Assert.True(r.Graph.Metadata.ContainsKey("goalCount"));
    }
}
