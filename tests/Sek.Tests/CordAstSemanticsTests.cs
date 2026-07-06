using System.Linq;
using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Cord.Semantics;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for the Cord AST helpers (<c>ReferencedTargets</c>/<c>FindConstruct</c> across every
/// behavior node), the semantic-phase diagnostics vocabulary, and the symbol table.
/// </summary>
public class CordAstSemanticsTests
{
    private static InvocationBehavior Inv(string t) => new() { Target = t };

    [Fact]
    public void ReferencedTargets_WalksEveryBehaviorNode()
    {
        var par = new ParallelBehavior { Op = "sync" };
        par.Items.Add(Inv("P1"));
        par.Items.Add(Inv("P2"));
        var perm = new PermutationBehavior();
        perm.Items.Add(Inv("Q1"));
        perm.Items.Add(Inv("Q2"));
        var loose = new LooseSequenceBehavior();
        loose.Items.Add(Inv("L1"));
        var seq = new SequenceBehavior();
        seq.Items.Add(par);
        seq.Items.Add(perm);
        seq.Items.Add(loose);
        seq.Items.Add(new RepetitionBehavior { Inner = Inv("R"), Op = "*" });
        seq.Items.Add(new GroupBehavior { Inner = Inv("G") });
        seq.Items.Add(new PreconstraintBehavior { Code = "x", Inner = Inv("PC") });
        seq.Items.Add(new FailBehavior { Inner = Inv("F") });
        seq.Items.Add(new LetBehavior { Inner = Inv("Le") });
        var bind = new BindBehavior { Inner = Inv("B") };
        seq.Items.Add(bind);

        var targets = seq.ReferencedTargets().ToList();
        foreach (var t in new[] { "P1", "P2", "Q1", "Q2", "L1", "R", "G", "PC", "F", "Le", "B" })
        {
            Assert.Contains(t, targets);
        }
    }

    [Fact]
    public void FindConstruct_LocatesConstructUnderEveryWrapper()
    {
        var construct = new ConstructBehavior { Kind = ConstructKind.ModelProgram, Reference = "C" };
        Assert.Same(construct, new RepetitionBehavior { Inner = construct, Op = "*" }.FindConstruct());
        Assert.Same(construct, new GroupBehavior { Inner = construct }.FindConstruct());
        Assert.Same(construct, new FailBehavior { Inner = construct }.FindConstruct());
        Assert.Same(construct, new LetBehavior { Inner = construct }.FindConstruct());
        Assert.Same(construct, new BindBehavior { Inner = construct }.FindConstruct());
        var perm = new PermutationBehavior();
        perm.Items.Add(construct);
        Assert.Same(construct, perm.FindConstruct());
        var loose = new LooseSequenceBehavior();
        loose.Items.Add(construct);
        Assert.Same(construct, loose.FindConstruct());
        Assert.Null(Inv("A").FindConstruct());
    }

    // ---- Diagnostics vocabulary --------------------------------------------------------

    [Fact]
    public void Diagnostic_ToString_PositionedAndUnpositioned()
    {
        var positioned = new Diagnostic(DiagnosticSeverity.Error, "SEM005", "bad ref", 12, 3);
        Assert.Contains("(12,3)", positioned.ToString());
        Assert.Contains("SEM005", positioned.ToString());
        var plain = new Diagnostic(DiagnosticSeverity.Warning, "SEM004", "unknown base");
        Assert.Contains("SEM004", plain.ToString());
        Assert.DoesNotContain("(", plain.ToString());
    }

    [Fact]
    public void DiagnosticBag_Info_Warning_Error()
    {
        var bag = new DiagnosticBag();
        bag.Info("I1", "info");
        bag.Warning("W1", "warn");
        bag.Error("E1", "err", 1, 1);
        Assert.Equal(3, bag.Items.Count);
        Assert.True(bag.HasErrors);
        Assert.Equal(1, bag.ErrorCount);
        Assert.Contains(bag.Items, d => d.Severity == DiagnosticSeverity.Info);
    }

    // ---- Semantic analyzer + symbol table ----------------------------------------------

    [Fact]
    public void SemanticAnalyzer_UnknownConfigBase_RaisesSEM003_Warning()
    {
        var model = SemanticAnalyzer.Analyze(CordDocument.ParseText(
            "config Derived : MissingBase { }\nmachine M() : Derived { construct model program from Derived }\n"));
        Assert.False(model.HasErrors); // config-base typo is a warning
        Assert.Contains(model.Diagnostics.Items, d => d.Code == "SEM003" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void SymbolTable_Enumerations_And_Lookups()
    {
        var model = SemanticAnalyzer.Analyze(CordDocument.ParseText(
            "config A { action all Svc; }\nconfig B : A { }\nmachine M() : B { construct model program from B }\n"));
        var st = model.Symbols;
        Assert.Equal(2, st.Configurations.Count);
        Assert.Single(st.Machines);
        Assert.NotNull(st.GetConfiguration("A"));
        Assert.NotNull(st.GetMachine("M"));
        Assert.Contains("Svc", st.ImportedActionTypes("M"));
        Assert.Empty(st.EventActions("M"));
    }
}
