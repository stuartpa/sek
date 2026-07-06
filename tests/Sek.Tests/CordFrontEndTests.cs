using System;
using System.IO;
using System.Linq;
using Sek.Cord;
using Sek.Cord.Ast;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for the <c>Sek.Cord</c> front end (lexer + parser + AST + <see cref="CordDocument"/>
/// resolution). Parses a broad spread of Cord constructs and every in-repo sample script
/// (readiness-gate coverage drive).
/// </summary>
public class CordFrontEndTests
{
    private static CordScript Parse(string src) => CordDocument.ParseText(src).Script;

    // ---- Behavior operators ------------------------------------------------------------

    [Fact]
    public void Parses_Sequence_Choice_Optional_Repetition_Groups()
    {
        var doc = Parse(
            "config C { action abstract static void I.A(); action abstract static void I.B(); action abstract static void I.Cc(); }\n" +
            "machine M() : C { ( (A; B) | Cc ); B* ; A? }\n");
        var m = doc.Machines.Single();
        Assert.NotNull(m.Body);
        // sequence at the top with a choice group, a repetition, and an optional
        var targets = m.Body!.ReferencedTargets().ToList();
        Assert.Contains("A", targets);
        Assert.Contains("B", targets);
        Assert.Contains("Cc", targets);
    }

    [Fact]
    public void Parses_SyncAndInterleaveParallel()
    {
        var doc = Parse(
            "config C { action abstract static void I.A(); action abstract static void I.B(); }\n" +
            "machine P() : C { A }\n" +
            "machine Q() : C { B }\n" +
            "machine Sync() : C { P || Q }\n" +
            "machine Inter() : C { P ||| Q }\n");
        Assert.Equal(4, doc.Machines.Count);
        Assert.IsType<ParallelBehavior>(Unwrap(doc.Machines.First(m => m.Name == "Sync").Body!));
        var inter = (ParallelBehavior)Unwrap(doc.Machines.First(m => m.Name == "Inter").Body!);
        Assert.Contains(inter.Op, new[] { "interleave", "sync-interleave", "|||" });
    }

    private static Behavior Unwrap(Behavior b) => b is GroupBehavior g ? Unwrap(g.Inner) : b;

    [Fact]
    public void Parses_InvocationArguments_AndReturnBinding()
    {
        var doc = Parse(
            "config C { action all Svc; }\n" +
            "machine M() : C { Open(1) / h; Write(h, \"data\"); Close(h) }\n");
        var targets = doc.Machines.Single().Body!.ReferencedTargets().ToList();
        Assert.Contains("Open", targets);
        Assert.Contains("Write", targets);
        Assert.Contains("Close", targets);
    }

    [Fact]
    public void Parses_ConstructModelProgram_WithScope()
    {
        var doc = Parse(
            "config Actions { action all Svc; }\n" +
            "machine ModelProgram() : Actions { construct model program from Actions where scope = \"Ns.Sub\" }\n");
        var construct = doc.Machines.Single().Body!.FindConstruct();
        Assert.NotNull(construct);
        Assert.Equal(ConstructKind.ModelProgram, construct!.Kind);
        Assert.Equal("Actions", construct.Reference);
        Assert.Equal("Ns.Sub", construct.Params["scope"]);
    }

    [Fact]
    public void Parses_ConstructTestCases_ForMachine()
    {
        var doc = Parse(
            "config Actions { action all Svc; }\n" +
            "machine ModelProgram() : Actions { construct model program from Actions }\n" +
            "machine TestSuite() : Actions { construct test cases where Strategy = \"longtests\" for ModelProgram }\n");
        var construct = doc.Machines.First(m => m.Name == "TestSuite").Body!.FindConstruct();
        Assert.Equal(ConstructKind.TestCases, construct!.Kind);
        Assert.Equal("ModelProgram", construct.Reference);
    }

    [Fact]
    public void Parses_Bind_And_Let()
    {
        var bindDoc = Parse(
            "config Actions { action all Svc; }\n" +
            "machine ModelProgram() : Actions { construct model program from Actions }\n" +
            "machine Bound() : Actions { ( bind Open({1}), Write(_, \"hi\") in ModelProgram ) }\n");
        Assert.Contains(bindDoc.Machines, m => m.Name == "Bound");

        var letDoc = Parse(
            "config Actions { action all Svc; }\n" +
            "machine ModelProgram() : Actions { construct model program from Actions }\n" +
            "machine Sliced() : Actions\n{\n\tlet int x, int y\n\t\twhere {.\n\t\t\tCondition.In(x, 1, 2);\n\t\t\tCondition.In(y, 3);\n\t\t.}\n\tin\n\tModelProgram\n}\n");
        Assert.Contains(letDoc.Machines, m => m.Name == "Sliced");
    }

    // ---- Action kinds ------------------------------------------------------------------

    [Fact]
    public void Parses_EventActionKind()
    {
        var doc = Parse(
            "config C { action all Svc; action event void Sub.Received(string data); }\n" +
            "machine M() : C { construct model program from C }\n");
        var evt = doc.Configurations.Single().DeclaredActions.Single(a => a.Target.EndsWith("Received"));
        Assert.Equal(ActionKind.Event, evt.Kind);
    }

    // ---- CordDocument resolution -------------------------------------------------------

    [Fact]
    public void ResolveSwitches_HonoursInheritanceAndOverrides()
    {
        var doc = CordDocument.ParseText(
            "config Base { switch StateBound = 100; switch StepBound = 50; }\n" +
            "config Derived : Base { switch StepBound = 999; }\n" +
            "machine M() : Derived where StateBound = 7 { construct model program from Derived }\n");
        var cfg = doc.ResolveConfigSwitches("Derived");
        Assert.Equal("100", cfg["StateBound"]);   // inherited
        Assert.Equal("999", cfg["StepBound"]);     // overridden
        var mach = doc.ResolveMachineSwitches("M");
        Assert.Equal("7", mach["StateBound"]);     // machine where-override wins
    }

    [Fact]
    public void ResolveDeclaredActions_And_ImportedTypes_AcrossBases()
    {
        var doc = CordDocument.ParseText(
            "config Base { action all AdapterA; action abstract static void I.Foo(); }\n" +
            "config Derived : Base { action all AdapterB; action abstract static void I.Bar(); }\n" +
            "machine M() : Derived { construct model program from Derived }\n");
        var actions = doc.ResolveMachineDeclaredActions("M");
        Assert.Contains("I.Foo", actions.Keys);
        Assert.Contains("I.Bar", actions.Keys);
        var imports = doc.ResolveMachineImportedActionTypes("M");
        Assert.Contains("AdapterA", imports);
        Assert.Contains("AdapterB", imports);
    }

    [Fact]
    public void ResolveMachineEventActions_ReturnsShortNames()
    {
        var doc = CordDocument.ParseText(
            "config C { action all Svc; action event void Sub.Received(string data); }\n" +
            "machine M() : C { construct model program from C }\n");
        var events = doc.ResolveMachineEventActions("M");
        Assert.Contains("Received", events);
    }

    [Fact]
    public void GetMachine_And_GetConfiguration_And_UnknownIsNull()
    {
        var doc = CordDocument.ParseText("config C { }\nmachine M() : C { construct model program from C }\n");
        Assert.NotNull(doc.GetConfiguration("C"));
        Assert.NotNull(doc.GetMachine("M"));
        Assert.Null(doc.GetConfiguration("Nope"));
        Assert.Null(doc.GetMachine("Nope"));
    }

    [Fact]
    public void AllDeclaredActionTargets_ListsEveryDeclaredAction()
    {
        var doc = CordDocument.ParseText(
            "config C { action abstract static void I.A(); action abstract static void I.B(); }\n");
        var targets = doc.AllDeclaredActionTargets().ToList();
        Assert.Contains("I.A", targets);
        Assert.Contains("I.B", targets);
    }

    // ---- Every real sample parses ------------------------------------------------------

    [Fact]
    public void EverySampleCordScript_ParsesWithoutError()
    {
        var repoRoot = FindRepoRoot();
        var samplesDir = Path.Combine(repoRoot, "samples");
        Assert.True(Directory.Exists(samplesDir), $"samples dir not found at {samplesDir}");

        var cordFiles = Directory.EnumerateFiles(samplesDir, "*.cord", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(cordFiles);
        foreach (var file in cordFiles)
        {
            var ex = Record.Exception(() => CordDocument.ParseText(File.ReadAllText(file)));
            Assert.True(ex is null, $"failed to parse {file}: {ex?.Message}");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
