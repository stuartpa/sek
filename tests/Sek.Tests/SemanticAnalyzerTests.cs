using System.Linq;
using Sek.Cord;
using Sek.Cord.Semantics;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for the semantic-analysis phase (ARC001, phase 3): the <see cref="SemanticAnalyzer"/>,
/// <see cref="SymbolTable"/>, and <see cref="DiagnosticBag"/>. Verifies that valid Cord produces no
/// errors and that each cross-reference defect raises its diagnostic code.
/// </summary>
public class SemanticAnalyzerTests
{
    private static SemanticModel Analyze(string cord, string? machine = null) =>
        SemanticAnalyzer.Analyze(CordDocument.ParseText(cord), machine);

    private const string Valid =
        "config Actions { action all Widget; }\n" +
        "machine ModelProgram() : Actions { construct model program from Actions }\n" +
        "machine TestSuite() : Actions { construct test cases for ModelProgram }\n";

    [Fact]
    public void Valid_Document_HasNoErrors()
    {
        var model = Analyze(Valid);
        Assert.False(model.HasErrors);
        Assert.Empty(model.Diagnostics.Items);
    }

    [Fact]
    public void SymbolTable_KnowsConfigsAndMachines()
    {
        var model = Analyze(Valid);
        Assert.True(model.Symbols.IsConfiguration("Actions"));
        Assert.True(model.Symbols.IsMachine("ModelProgram"));
        Assert.True(model.Symbols.IsMachine("TestSuite"));
        Assert.False(model.Symbols.IsMachine("Actions"));
        Assert.True(model.Symbols.IsConstructReference("Actions"));     // config
        Assert.True(model.Symbols.IsConstructReference("ModelProgram")); // machine
        Assert.False(model.Symbols.IsConstructReference("Nope"));
    }

    [Fact]
    public void DuplicateConfiguration_RaisesSEM001()
    {
        var model = Analyze("config A { } config A { }\nmachine M() : A { construct model program from A }\n");
        Assert.True(model.HasErrors);
        Assert.Contains(model.Diagnostics.Items, d => d.Code == "SEM001");
    }

    [Fact]
    public void DuplicateMachine_RaisesSEM002()
    {
        var model = Analyze(
            "config A { }\n" +
            "machine M() : A { construct model program from A }\n" +
            "machine M() : A { construct model program from A }\n");
        Assert.True(model.HasErrors);
        Assert.Contains(model.Diagnostics.Items, d => d.Code == "SEM002");
    }

    [Fact]
    public void UnknownBaseConfig_RaisesSEM004_Warning_NotError()
    {
        var model = Analyze("config A { }\nmachine M() : Missing { construct model program from A }\n");
        Assert.False(model.HasErrors); // base-config typos are warnings, not fatal
        Assert.Contains(model.Diagnostics.Items, d => d.Code == "SEM004" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void UnknownConstructReference_RaisesSEM005()
    {
        var model = Analyze("config A { }\nmachine M() : A { construct model program from DoesNotExist }\n");
        Assert.True(model.HasErrors);
        Assert.Contains(model.Diagnostics.Items, d => d.Code == "SEM005");
    }

    [Fact]
    public void UnknownTargetMachine_RaisesSEM006()
    {
        var model = Analyze(Valid, machine: "NoSuchMachine");
        Assert.True(model.HasErrors);
        Assert.Contains(model.Diagnostics.Items, d => d.Code == "SEM006");
    }

    [Fact]
    public void KnownTargetMachine_IsAccepted()
    {
        var model = Analyze(Valid, machine: "ModelProgram");
        Assert.False(model.HasErrors);
    }

    [Fact]
    public void DiagnosticBag_TracksSeverityAndErrorState()
    {
        var bag = new DiagnosticBag();
        Assert.False(bag.HasErrors);
        bag.Warning("W1", "just advisory");
        Assert.False(bag.HasErrors);
        Assert.Equal(0, bag.ErrorCount);
        bag.Error("E1", "fatal");
        Assert.True(bag.HasErrors);
        Assert.Equal(1, bag.ErrorCount);
        Assert.Equal(2, bag.Items.Count);
    }
}
