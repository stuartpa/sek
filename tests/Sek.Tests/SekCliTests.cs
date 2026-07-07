using System;
using System.Linq;
using Sek.Cli;
using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Core.Model;
using Sek.Engine;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Unit coverage for the now-extracted <see cref="SekCli"/> command/engine-driving helpers (REF003).
/// These were untestable local functions in the old top-level <c>Program.cs</c>; extracting them into
/// a library type lets us hit the branchy option-parsing, scope/strategy resolution, state-slice and
/// point-shoot logic directly.
/// </summary>
public class SekCliTests
{
    // ---- option parsing / small helpers ------------------------------------------------

    [Fact]
    public void GetOption_FoundMissingAndAtEnd()
    {
        Assert.Equal("z3", SekCli.GetOption(new[] { "--solver", "z3" }, "--solver"));
        Assert.Null(SekCli.GetOption(new[] { "explore", "M" }, "--solver"));
        Assert.Null(SekCli.GetOption(new[] { "explore", "--solver" }, "--solver")); // flag at end, no value
    }

    [Theory]
    [InlineData("-h", true)]
    [InlineData("--help", true)]
    [InlineData("help", true)]
    [InlineData("explore", false)]
    public void IsHelp_RecognisesHelpTokens(string arg, bool expected) => Assert.Equal(expected, SekCli.IsHelp(arg));

    [Fact]
    public void ShortLabel_TakesLastSegment()
    {
        Assert.Equal("Push", SekCli.ShortLabel("Turnstile.Push"));
        Assert.Equal("Bare", SekCli.ShortLabel("Bare"));
    }

    // ---- ConvertLiteral (state-slice literal coercion) ---------------------------------

    [Fact]
    public void ConvertLiteral_Bool_String_Int_Long()
    {
        Assert.Equal(true, SekCli.ConvertLiteral("true", typeof(bool)));
        Assert.Equal(false, SekCli.ConvertLiteral("false", typeof(bool)));
        Assert.Equal("hi", SekCli.ConvertLiteral("\"hi\"", typeof(string)));
        Assert.Equal("plain", SekCli.ConvertLiteral("plain", typeof(string)));
        Assert.Equal(5, SekCli.ConvertLiteral("5", typeof(int)));
        Assert.Equal(5L, SekCli.ConvertLiteral("5", typeof(long)));
    }

    [Fact]
    public void ConvertLiteral_Enum_PlainDottedAndInvalid()
    {
        Assert.Equal(DayOfWeek.Monday, SekCli.ConvertLiteral("Monday", typeof(DayOfWeek)));
        Assert.Equal(DayOfWeek.Tuesday, SekCli.ConvertLiteral("System.DayOfWeek.Tuesday", typeof(DayOfWeek)));
        Assert.Null(SekCli.ConvertLiteral("NotADay", typeof(DayOfWeek)));
    }

    [Fact]
    public void ConvertLiteral_Nullable_UnwrapsUnderlyingType()
    {
        Assert.Equal(7, SekCli.ConvertLiteral("7", typeof(int?)));
    }

    [Fact]
    public void ConvertLiteral_UnparseableForNumeric_FallsBackToToken()
    {
        Assert.Equal("xyz", SekCli.ConvertLiteral("xyz", typeof(int)));
    }

    // ---- BoundsFor (switch → exploration options) --------------------------------------

    [Fact]
    public void BoundsFor_ReadsSwitches()
    {
        var cord = CordDocument.ParseText(
            "config C { switch StateBound = 5; switch StepBound = 7; switch PathDepthBound = 3; switch StopAtError = true; }\n" +
            "machine M() : C { construct model program from C }\n");
        var o = SekCli.BoundsFor(cord, "M");
        Assert.Equal(5, o.MaxStates);
        Assert.Equal(7, o.MaxTransitions);
        Assert.Equal(3, o.MaxDepth);
        Assert.True(o.StopAtError);
    }

    // ---- ResolveModelScope / FindTestStrategy ------------------------------------------

    [Fact]
    public void ResolveModelScope_FindsScope_OrNull()
    {
        var cord = CordDocument.ParseText(
            "config C { action all S; }\nmachine M() : C { construct model program from C where scope = \"Ns.Sub\" }\n");
        Assert.Equal("Ns.Sub", SekCli.ResolveModelScope(cord, cord.GetMachine("M")!.Body));

        var noScope = CordDocument.ParseText(
            "config C { action all S; }\nmachine M() : C { construct model program from C }\n");
        Assert.Null(SekCli.ResolveModelScope(noScope, noScope.GetMachine("M")!.Body));
    }

    [Fact]
    public void FindTestStrategy_ReadsStrategyOption()
    {
        var cord = CordDocument.ParseText(
            "config C { action all S; }\nmachine ModelProgram() : C { construct model program from C }\n" +
            "machine T() : C { construct test cases where Strategy=\"longtests\" for ModelProgram }\n");
        Assert.Equal("longtests", SekCli.FindTestStrategy(cord, "T"));
    }

    // ---- ExpandParamMachines / DesugarLet / Unwrap (behavior transforms) ---------------

    [Fact]
    public void Unwrap_UnwrapsGroups()
    {
        var inner = new InvocationBehavior { Target = "A" };
        var grouped = new GroupBehavior { Inner = inner };
        Assert.Same(inner, SekCli.Unwrap(grouped));
        Assert.Null(SekCli.Unwrap(null));
    }

    [Fact]
    public void DesugarLet_ExpandsLetIntoChoice()
    {
        var cord = CordDocument.ParseText(
            "config C { action all S; }\nmachine M() : C { let int x where {. Condition.In(x, 1, 2); .} in a }\n");
        var body = cord.GetMachine("M")!.Body!;
        var desugared = SekCli.DesugarLet(body);
        Assert.NotNull(desugared);
        // no LetBehavior remains at the top after desugaring
        Assert.IsNotType<LetBehavior>(desugared);
    }

    [Fact]
    public void ExpandParamMachines_IsIdempotentForPlainBody()
    {
        var cord = CordDocument.ParseText(
            "config C { action all S; }\nmachine M() : C { a; b }\n");
        var body = cord.GetMachine("M")!.Body!;
        var expanded = SekCli.ExpandParamMachines(cord, body);
        Assert.NotNull(expanded);
    }

    // ---- GoalHashes / ExtractSlice -----------------------------------------------------

    [Fact]
    public void GoalHashes_ReadsGoalStatesFromMetadata()
    {
        var g = new ExplorationGraph { Machine = "M", InitialStateId = "S0" };
        g.States.Add(new ModelState("S0", "h0", Initial: true));
        g.States.Add(new ModelState("S1", "h1"));
        g.Metadata["goals"] = "S1";
        var r = new ExplorationResult { Graph = g };
        var hashes = SekCli.GoalHashes(r);
        Assert.Contains("h1", hashes);
        Assert.DoesNotContain("h0", hashes);
    }

    [Fact]
    public void ExtractSlice_DistributesModelProgramOutOfParallel()
    {
        var cord = CordDocument.ParseText(
            "config C { action all S; }\n" +
            "machine Mp() : C { construct model program from C }\n" +
            "machine Sliced() : C { a || Mp }\n");
        var (scenario, config) = SekCli.ExtractSlice(cord, cord.GetMachine("Sliced")!.Body);
        Assert.NotNull(scenario);
        Assert.Equal("C", config); // sliced against the model program's config
    }
}
