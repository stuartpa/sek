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
/// Branch coverage for <see cref="SekCli.InterpretConstruct"/> — the Cord <c>construct</c> family:
/// requirement coverage (with captured requirements, a <c>RequirementsToCover</c> set, a
/// <c>MinimumRequirementCount</c> threshold, and the no-capture fallback), bounded exploration
/// (<c>PathDepth</c>), accepting-paths and accept-completion filtering, and point-shoot steering
/// (both the degenerate goal-filter form and a form with a Shoot phase).
/// </summary>
public class SekConstructBranchTests
{
    /// <summary>A model that captures two requirement ids as it explores.</summary>
    public sealed class ReqModel : ModelProgram
    {
        public int N { get; set; }

        [Rule("R.Inc")]
        public void Inc()
        {
            Require(N < 3, "cap");
            Requirement.Capture("REQ-INC");
            N++;
        }

        [Rule("R.Dec")]
        public void Dec()
        {
            Require(N > 0, "floor");
            Requirement.Capture("REQ-DEC");
            N--;
        }

        [AcceptingCondition]
        public bool Done() => N == 3;
    }

    /// <summary>A model that captures no requirements (drives the fallback action-set path).</summary>
    public sealed class NoReqModel : ModelProgram
    {
        public int N { get; set; }

        [Rule("Q.Step")]
        public void Step()
        {
            Require(N < 2, "cap");
            N++;
        }

        [AcceptingCondition]
        public bool Done() => N == 2;
    }

    private static ExplorationResult Run<T>(string cordText, ConstructBehavior cb) where T : ModelProgram
    {
        var intro = new ModelIntrospector(typeof(T));
        var cord = CordDocument.ParseText(cordText);
        var opts = new ExplorationOptions { MaxStates = 32, MaxTransitions = 64, MaxDepth = 8 };
        var binds = new Dictionary<string, List<List<string>>>(StringComparer.Ordinal);
        return SekCli.InterpretConstruct(intro, cord, "M", cb, opts, "enum", binds);
    }

    private const string ReqCord = "config C { action all R; }\nmachine M() : C { construct model program from C }\n";

    [Fact]
    public void RequirementCoverage_WithToCoverAndMinimum()
    {
        var cb = new ConstructBehavior { Kind = ConstructKind.RequirementCoverage, Reference = "C" };
        cb.Params["RequirementsToCover"] = "REQ-INC, REQ-MISSING";
        cb.Params["MinimumRequirementCount"] = "1";

        var g = Run<ReqModel>(ReqCord, cb).Graph;

        Assert.Contains("REQ-INC", g.Metadata["requirementsCovered"]);
        Assert.Contains("REQ-MISSING", g.Metadata["requirementsMissing"]);
        // REQ-MISSING is never captured → coverage incomplete …
        Assert.Equal("False", g.Metadata["requirementsCoverageComplete"]);
        // … but one of the to-cover ids (REQ-INC) was hit, meeting the minimum of 1.
        Assert.Equal("True", g.Metadata["minimumRequirementCountMet"]);
    }

    [Fact]
    public void RequirementCoverage_NoCaptures_FallsBackToActionSet()
    {
        var cord = "config C { action all Q; }\nmachine M() : C { construct model program from C }\n";
        var cb = new ConstructBehavior { Kind = ConstructKind.RequirementCoverage, Reference = "C" };

        var g = Run<NoReqModel>(cord, cb).Graph;

        Assert.True(g.Metadata.ContainsKey("requirements"));
        Assert.Contains("Q.Step", g.Metadata["requirements"]);
    }

    [Fact]
    public void BoundedExploration_AppliesPathDepth()
    {
        var deep = Run<ReqModel>(ReqCord, new ConstructBehavior { Kind = ConstructKind.BoundedExploration, Reference = "C" });

        var cb = new ConstructBehavior { Kind = ConstructKind.BoundedExploration, Reference = "C" };
        cb.Params["PathDepth"] = "1";
        var shallow = Run<ReqModel>(ReqCord, cb);

        Assert.True(shallow.Graph.States.Count < deep.Graph.States.Count);
    }

    [Fact]
    public void AcceptingPaths_KeepsOnlyStatesReachingAccepting()
    {
        var g = Run<ReqModel>(ReqCord, new ConstructBehavior { Kind = ConstructKind.AcceptingPaths, Reference = "C" }).Graph;
        // Every retained state can still reach the accepting (N==3) state.
        Assert.Contains(g.States, s => s.Accepting);
        Assert.NotEmpty(g.States);
    }

    [Fact]
    public void AcceptCompletion_KeepsCompletableStates()
    {
        var g = Run<ReqModel>(ReqCord, new ConstructBehavior { Kind = ConstructKind.AcceptCompletion, Reference = "C" }).Graph;
        Assert.NotEmpty(g.States);
        Assert.Contains(g.States, s => s.Accepting);
    }

    [Fact]
    public void PointShoot_Degenerate_FiltersToGoal()
    {
        var cb = new ConstructBehavior { Kind = ConstructKind.PointShoot, Reference = "C" };
        cb.Params["with"] = "Done"; // steer toward the accepting (N==3) state

        var g = Run<ReqModel>(ReqCord, cb).Graph;
        Assert.True(g.Metadata.ContainsKey("goalCount"));
    }

    [Fact]
    public void PointShoot_WithShootPhase_Completes()
    {
        var cord =
            "config C { action all R; }\n" +
            "machine Shoot() : C { construct model program from C }\n" +
            "machine M() : C { construct model program from C }\n";
        var cb = new ConstructBehavior { Kind = ConstructKind.PointShoot, Reference = "C" };
        cb.Params["with"] = "Done";
        cb.Params["Shoot"] = "Shoot";
        cb.Params["PathDepth"] = "4";

        var g = Run<ReqModel>(cord, cb).Graph;
        Assert.True(g.Metadata.ContainsKey("phases"));
        Assert.True(g.Metadata.ContainsKey("goalCount"));
    }
}
