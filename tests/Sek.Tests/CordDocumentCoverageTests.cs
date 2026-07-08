using System.Linq;
using Sek.Cord;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for <see cref="CordDocument"/>'s machine-scoped resolution helpers: the
/// unknown-machine (null) early-returns and the <c>action event</c> short-label extraction.
/// </summary>
public class CordDocumentCoverageTests
{
    private static CordDocument Doc() => CordDocument.ParseText(
        "config C {\n" +
        "  action all S;\n" +
        "  action event void S.Ev();\n" +
        "  switch K = 5;\n" +
        "}\n" +
        "machine M() : C { construct model program from C }\n");

    [Fact]
    public void MachineScopedResolvers_KnownMachine()
    {
        var d = Doc();
        Assert.Equal("5", d.ResolveMachineSwitches("M")["K"]);
        Assert.Contains("S.Ev", d.ResolveMachineDeclaredActions("M").Keys);
        Assert.Contains("S", d.ResolveMachineImportedActionTypes("M"));
        Assert.Contains("Ev", d.ResolveMachineEventActions("M")); // event → short label
    }

    [Fact]
    public void MachineScopedResolvers_UnknownMachine_AreEmpty()
    {
        var d = Doc();
        Assert.Empty(d.ResolveMachineSwitches("Nope"));
        Assert.Empty(d.ResolveMachineDeclaredActions("Nope"));
        Assert.Empty(d.ResolveMachineImportedActionTypes("Nope"));
        Assert.Empty(d.ResolveMachineEventActions("Nope"));
    }

    [Fact]
    public void EventAction_WithoutDot_UsesWholeTarget()
    {
        // An event action declared without a Type. qualifier (bare method) has a dotless target,
        // exercising the `LastIndexOf('.') < 0` arm of the short-label extraction.
        var d = CordDocument.ParseText(
            "config C { action all S; action event void Ev(); }\n" +
            "machine M() : C { construct model program from C }\n");
        var events = d.ResolveMachineEventActions("M");
        Assert.Contains("Ev", events);
    }
}
