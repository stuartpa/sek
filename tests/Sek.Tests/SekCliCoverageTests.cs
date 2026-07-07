using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sek.Cli;
using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Core.Model;
using Sek.Engine;
using Sek.Modeling;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch-coverage for the extracted <see cref="SekCli"/> command handlers and the recursive
/// Cord AST transforms (scope resolution, strategy lookup, slice extraction, let-desugaring,
/// param-machine expansion, substitution). These exercise the rarer behavior-node kinds
/// (permutation / loose-sequence / fail / preconstraint) and the command error paths that the
/// sample explorations do not reach.
/// </summary>
public class SekCliCoverageTests : IClassFixture<SampleModelsFixture>
{
    private readonly SampleModelsFixture _fx;
    private readonly string _turnstile;

    public SekCliCoverageTests(SampleModelsFixture fx)
    {
        _fx = fx;
        _turnstile = Path.Combine(fx.RepoRoot, "samples", "Turnstile");
    }

    private static CordDocument Cord(string text) => CordDocument.ParseText(text);

    private static InvocationBehavior Inv(string target, params string[] args) =>
        new() { Target = target, Args = args.Length == 0 ? null : args.ToList() };

    private static ConstructBehavior ScopeModel(string scope)
    {
        var cb = new ConstructBehavior { Kind = ConstructKind.ModelProgram, Reference = "C" };
        cb.Params["scope"] = scope;
        return cb;
    }

    // ---- command aliases + top-level dispatch (CliHost.Run) -----------------------------

    [Fact]
    public void RunAlias_ExploresAndReplays_LikeTest()
    {
        var (code, _, err) = CliHost.Run("run", "ModelProgram", "--project", _turnstile);
        Assert.True(code == 0, err);
    }

    [Fact]
    public void GenAlias_GeneratesTests_LikeGenerate()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "sekcov_gen_" + Guid.NewGuid().ToString("N"));
        try
        {
            var (code, _, err) = CliHost.Run("gen", "ModelProgram", "--project", _turnstile, "--out", outDir, "--max", "2");
            Assert.True(code == 0, err);
        }
        finally { TryDeleteDir(outDir); }
    }

    [Fact]
    public void Version_And_Z3_And_Usage()
    {
        Assert.Equal(0, CliHost.Run("version").Code);
        Assert.Equal(0, CliHost.Run("z3").Code);
        Assert.Equal(1, CliHost.Run("bogus-command").Code);
        // no args → usage banner, exit 0
        Assert.Equal(0, CliHost.Run().Code);
    }

    // ---- view: formats, stdout, and errors ---------------------------------------------

    [Fact]
    public void View_HtmlAndDot_ToStdout_AndUnknownFormatErrors()
    {
        // Produce a graph to view.
        Assert.Equal(0, CliHost.Run("explore", "ModelProgram", "--project", _turnstile).Code);
        var seexpl = Path.Combine(_turnstile, ".specexplorerkit", "out", "ModelProgram.seexpl");

        Assert.Equal(0, CliHost.Run("view", seexpl, "--format", "html").Code);
        Assert.Equal(0, CliHost.Run("view", seexpl, "--format", "dot").Code);
        Assert.Equal(0, CliHost.Run("view", seexpl, "--format", "mmd").Code);

        var (code, _, err) = CliHost.Run("view", seexpl, "--format", "nonsense");
        Assert.Equal(1, code);
        Assert.Contains("unknown --format", err);
    }

    [Fact]
    public void View_MissingFile_And_NoArgs()
    {
        var missing = Path.Combine(Path.GetTempPath(), "sekcov_missing_" + Guid.NewGuid().ToString("N") + ".seexpl");
        var (code, _, err) = CliHost.Run("view", missing);
        Assert.Equal(1, code);
        Assert.Contains("file not found", err);

        // no args → usage, exit 1
        Assert.Equal(1, CliHost.Run("view").Code);
        // help token → usage, exit 0
        Assert.Equal(0, CliHost.Run("view", "--help").Code);
    }

    // ---- init: writes a fresh template config ------------------------------------------

    [Fact]
    public void Init_FreshDir_WritesTemplate_ThenIdempotent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sekcov_init_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var (code, outText, _) = CliHost.Run("init", "--project", dir);
            Assert.Equal(0, code);
            Assert.Contains("Initialized", outText);
            Assert.True(File.Exists(Path.Combine(dir, ".specexplorerkit", "config.json")));

            // Second run: already initialized.
            var (code2, outText2, _) = CliHost.Run("init", "--project", dir);
            Assert.Equal(0, code2);
            Assert.Contains("already initialized", outText2);
        }
        finally { TryDeleteDir(dir); }
    }

    // ---- test / generate error branches (cord-only temp project, no binding) -----------

    [Fact]
    public void Test_And_Generate_MachineNotFound_And_NoBinding()
    {
        var dir = MakeCordOnlyProject(withBinding: false);
        try
        {
            // machine not found
            var (tc, _, terr) = CliHost.Run("test", "NoSuchMachine", "--project", dir);
            Assert.Equal(1, tc);
            Assert.Contains("not found", terr);

            var (gc, _, gerr) = CliHost.Run("generate", "NoSuchMachine", "--project", dir);
            Assert.Equal(1, gc);
            Assert.Contains("not found", gerr);

            // machine found but no binding configured
            var (tc2, _, terr2) = CliHost.Run("test", "ModelProgram", "--project", dir);
            Assert.Equal(1, tc2);
            Assert.Contains("binding", terr2);

            var (gc2, _, gerr2) = CliHost.Run("generate", "ModelProgram", "--project", dir);
            Assert.Equal(1, gc2);
            Assert.Contains("binding", gerr2);
        }
        finally { TryDeleteDir(dir); }
    }

    [Fact]
    public void Test_And_Generate_And_Explore_HelpAndNoArgs()
    {
        Assert.Equal(1, CliHost.Run("test").Code);
        Assert.Equal(0, CliHost.Run("test", "--help").Code);
        Assert.Equal(1, CliHost.Run("generate").Code);
        Assert.Equal(0, CliHost.Run("generate", "--help").Code);
        Assert.Equal(1, CliHost.Run("explore").Code);
        Assert.Equal(0, CliHost.Run("explore", "--help").Code);
    }

    [Fact]
    public void Explore_MachineNotFound_Errors()
    {
        var (code, _, err) = CliHost.Run("explore", "NoSuchMachine", "--project", _turnstile);
        Assert.Equal(1, code);
        Assert.Contains("not found", err);
    }

    // ---- ResolveModelScope: every container node kind ----------------------------------

    [Fact]
    public void ResolveModelScope_WalksAllContainerKinds()
    {
        var cord = Cord("config C { }\nmachine M() : C { construct model program from C }\n");
        var leaf = ScopeModel("Target.Ns");

        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, leaf));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new SequenceBehavior { Items = { leaf } }));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new ChoiceBehavior { Items = { leaf } }));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new ParallelBehavior { Items = { leaf } }));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new RepetitionBehavior { Inner = leaf }));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new GroupBehavior { Inner = leaf }));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new PreconstraintBehavior { Code = "x", Inner = leaf }));
        var bind = new BindBehavior { Inner = leaf };
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, bind));
        Assert.Equal("Target.Ns", SekCli.ResolveModelScope(cord, new ConstructBehavior { Kind = ConstructKind.TestCases, Target = leaf }));

        // null body and a default (unhandled) node → null
        Assert.Null(SekCli.ResolveModelScope(cord, null));
        Assert.Null(SekCli.ResolveModelScope(cord, Inv("SomeAction")));
        // invocation to a known machine recurses
        Assert.Null(SekCli.ResolveModelScope(cord, Inv("M")));
    }

    // ---- FindTestStrategy: strategy key, target, machine refs, containers ---------------

    [Fact]
    public void FindTestStrategy_ResolvesThroughConstructsAndContainers()
    {
        var cord = Cord(
            "config C { }\n" +
            "machine Base() : C { construct model program from C }\n" +
            "machine Suite() : C { construct test cases where Strategy = \"longtests\" for Base }\n");
        Assert.Equal("longtests", SekCli.FindTestStrategy(cord, "Suite"));
        Assert.Null(SekCli.FindTestStrategy(cord, "Base"));

        // Direct AST: test-cases with a Target (no Strategy) recurses into the target.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var withStrategy = new ConstructBehavior { Kind = ConstructKind.TestCases };
        withStrategy.Params["Strategy"] = "shorttests";
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, withStrategy, seen));

        var tcTarget = new ConstructBehavior { Kind = ConstructKind.TestCases, Target = withStrategy };
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, tcTarget, new HashSet<string>(StringComparer.Ordinal)));

        var otherTarget = new ConstructBehavior { Kind = ConstructKind.AcceptingPaths, Target = withStrategy };
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, otherTarget, new HashSet<string>(StringComparer.Ordinal)));

        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, new ParallelBehavior { Items = { withStrategy } }, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, new SequenceBehavior { Items = { withStrategy } }, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, new ChoiceBehavior { Items = { withStrategy } }, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, new GroupBehavior { Inner = withStrategy }, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("shorttests", SekCli.FindTestStrategyIn(cord, new BindBehavior { Inner = withStrategy }, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Null(SekCli.FindTestStrategyIn(cord, Inv("SomeAction"), new HashSet<string>(StringComparer.Ordinal)));
    }

    // ---- ExtractSlice: both parallel orders, choice, repetition, machine inline ---------

    [Fact]
    public void ExtractSlice_ParallelBothOrders_ChoiceAndRepetition()
    {
        var cord = Cord("config C { }\nmachine M() : C { construct model program from C }\n");
        ConstructBehavior Model() => new() { Kind = ConstructKind.ModelProgram, Reference = "C" };

        // model on the LEFT, scenario on the right
        var p1 = new ParallelBehavior();
        p1.Items.Add(Model());
        p1.Items.Add(Inv("A"));
        var (s1, c1) = SekCli.ExtractSlice(cord, p1);
        Assert.Equal("C", c1);
        Assert.NotNull(s1);

        // model on the RIGHT, scenario on the left
        var p2 = new ParallelBehavior();
        p2.Items.Add(Inv("A"));
        p2.Items.Add(Model());
        var (_, c2) = SekCli.ExtractSlice(cord, p2);
        Assert.Equal("C", c2);

        // both model programs → genuine parallel; neither side yields a slice config
        var p3 = new ParallelBehavior();
        p3.Items.Add(Model());
        p3.Items.Add(Model());
        var (s3, c3) = SekCli.ExtractSlice(cord, p3);
        Assert.Null(c3);
        Assert.IsType<ParallelBehavior>(s3);

        // choice + repetition carry the config through
        var choice = new ChoiceBehavior();
        choice.Items.Add(p1);
        var (_, cc) = SekCli.ExtractSlice(cord, choice);
        Assert.Equal("C", cc);

        var rep = new RepetitionBehavior { Inner = p1, Op = "*" };
        var (sr, cr) = SekCli.ExtractSlice(cord, rep);
        Assert.Equal("C", cr);
        Assert.IsType<RepetitionBehavior>(sr);

        // machine reference inlines
        var (_, cm) = SekCli.ExtractSlice(cord, Inv("M"));
        Assert.Null(cm); // M is a bare model program, not a slice
        // null → empty sequence
        var (sn, cn) = SekCli.ExtractSlice(cord, null);
        Assert.IsType<SequenceBehavior>(sn);
        Assert.Null(cn);
    }

    [Fact]
    public void IsPureModelProgram_And_ResolveSliceItem()
    {
        var cord = Cord("config C { }\nmachine M() : C { construct model program from C }\n");
        Assert.Equal("C", SekCli.IsPureModelProgram(cord, Inv("M")));
        Assert.Null(SekCli.IsPureModelProgram(cord, Inv("SomeAction")));

        var body = SekCli.ResolveSliceItem(cord, Inv("M"));
        Assert.NotNull(body);
        Assert.NotNull(SekCli.ResolveSliceItem(cord, new GroupBehavior { Inner = Inv("M") }));
        var plain = Inv("SomeAction");
        Assert.Same(plain, SekCli.ResolveSliceItem(cord, plain));
    }

    // ---- The recursive transforms over the rarer node kinds ----------------------------

    [Fact]
    public void ExpandParamMachines_CoversAllNodeKinds()
    {
        var cord = Cord(
            "config C { }\n" +
            "machine P(int id) : C { A(id) }\n");
        // param-machine expansion substitutes the argument into the invocation's args
        var expanded = SekCli.ExpandParamMachines(cord, Inv("P", "5"));
        Assert.Equal("5", ArgOf(expanded));

        // each container kind round-trips through expansion
        Assert.IsType<PermutationBehavior>(SekCli.ExpandParamMachines(cord, new PermutationBehavior { Items = { Inv("A") } }));
        Assert.IsType<LooseSequenceBehavior>(SekCli.ExpandParamMachines(cord, new LooseSequenceBehavior { Items = { Inv("A") } }));
        Assert.IsType<FailBehavior>(SekCli.ExpandParamMachines(cord, new FailBehavior { Inner = Inv("A") }));
        Assert.IsType<PreconstraintBehavior>(SekCli.ExpandParamMachines(cord, new PreconstraintBehavior { Code = "x", Inner = Inv("A") }));
        Assert.IsType<RepetitionBehavior>(SekCli.ExpandParamMachines(cord, new RepetitionBehavior { Inner = Inv("A") }));
        Assert.IsType<GroupBehavior>(SekCli.ExpandParamMachines(cord, new GroupBehavior { Inner = Inv("A") }));
        Assert.IsType<SequenceBehavior>(SekCli.ExpandParamMachines(cord, new SequenceBehavior { Items = { Inv("A") } }));
        Assert.IsType<ChoiceBehavior>(SekCli.ExpandParamMachines(cord, new ChoiceBehavior { Items = { Inv("A") } }));
        Assert.IsType<ParallelBehavior>(SekCli.ExpandParamMachines(cord, new ParallelBehavior { Items = { Inv("A") } }));
    }

    [Fact]
    public void ReferencesVars_CoversAllNodeKinds()
    {
        var vars = new HashSet<string>(StringComparer.Ordinal) { "id" };
        Assert.True(SekCli.ReferencesVars(Inv("A", "id"), vars));
        Assert.False(SekCli.ReferencesVars(Inv("A", "other"), vars));
        Assert.True(SekCli.ReferencesVars(new PermutationBehavior { Items = { Inv("A", "id") } }, vars));
        Assert.True(SekCli.ReferencesVars(new LooseSequenceBehavior { Items = { Inv("A", "id") } }, vars));
        Assert.True(SekCli.ReferencesVars(new FailBehavior { Inner = Inv("A", "id") }, vars));
        Assert.True(SekCli.ReferencesVars(new PreconstraintBehavior { Code = "x", Inner = Inv("A", "id") }, vars));
        Assert.True(SekCli.ReferencesVars(new RepetitionBehavior { Inner = Inv("A", "id") }, vars));
        Assert.True(SekCli.ReferencesVars(new GroupBehavior { Inner = Inv("A", "id") }, vars));
        Assert.True(SekCli.ReferencesVars(new SequenceBehavior { Items = { Inv("A", "id") } }, vars));
        Assert.True(SekCli.ReferencesVars(new ChoiceBehavior { Items = { Inv("A", "id") } }, vars));
        Assert.True(SekCli.ReferencesVars(new ParallelBehavior { Items = { Inv("A", "id") } }, vars));
        Assert.False(SekCli.ReferencesVars(ScopeModel("X"), vars)); // default arm
    }

    [Fact]
    public void CloneWithSubst_CoversAllNodeKinds_AndSubstitutes()
    {
        var subst = new Dictionary<string, string>(StringComparer.Ordinal) { ["id"] = "9" };
        var cloned = SekCli.CloneWithSubst(Inv("A", "id"), subst);
        Assert.Equal("9", ArgOf(cloned));

        Assert.IsType<PermutationBehavior>(SekCli.CloneWithSubst(new PermutationBehavior { Items = { Inv("A", "id") } }, subst));
        Assert.IsType<LooseSequenceBehavior>(SekCli.CloneWithSubst(new LooseSequenceBehavior { Items = { Inv("A", "id") } }, subst));
        Assert.IsType<FailBehavior>(SekCli.CloneWithSubst(new FailBehavior { Inner = Inv("A", "id") }, subst));
        Assert.IsType<PreconstraintBehavior>(SekCli.CloneWithSubst(new PreconstraintBehavior { Code = "x", Inner = Inv("A", "id") }, subst));
        Assert.IsType<RepetitionBehavior>(SekCli.CloneWithSubst(new RepetitionBehavior { Inner = Inv("A", "id") }, subst));
        Assert.IsType<GroupBehavior>(SekCli.CloneWithSubst(new GroupBehavior { Inner = Inv("A", "id") }, subst));
        Assert.IsType<SequenceBehavior>(SekCli.CloneWithSubst(new SequenceBehavior { Items = { Inv("A", "id") } }, subst));
        Assert.IsType<ChoiceBehavior>(SekCli.CloneWithSubst(new ChoiceBehavior { Items = { Inv("A", "id") } }, subst));
        Assert.IsType<ParallelBehavior>(SekCli.CloneWithSubst(new ParallelBehavior { Items = { Inv("A", "id") } }, subst));
    }

    [Fact]
    public void DesugarLet_CoversContainersAndBind()
    {
        // Container kinds without a let pass through preserving kind.
        Assert.IsType<PermutationBehavior>(SekCli.DesugarLet(new PermutationBehavior { Items = { Inv("A") } }));
        Assert.IsType<LooseSequenceBehavior>(SekCli.DesugarLet(new LooseSequenceBehavior { Items = { Inv("A") } }));
        Assert.IsType<FailBehavior>(SekCli.DesugarLet(new FailBehavior { Inner = Inv("A") }));
        Assert.IsType<PreconstraintBehavior>(SekCli.DesugarLet(new PreconstraintBehavior { Code = "x", Inner = Inv("A") }));
        Assert.IsType<RepetitionBehavior>(SekCli.DesugarLet(new RepetitionBehavior { Inner = Inv("A") }));
        Assert.IsType<GroupBehavior>(SekCli.DesugarLet(new GroupBehavior { Inner = Inv("A") }));
        Assert.IsType<BindBehavior>(SekCli.DesugarLet(new BindBehavior { Inner = Inv("A") }));
        Assert.IsType<SequenceBehavior>(SekCli.DesugarLet(new SequenceBehavior { Items = { Inv("A") } }));
        Assert.IsType<ChoiceBehavior>(SekCli.DesugarLet(new ChoiceBehavior { Items = { Inv("A") } }));
        Assert.IsType<ParallelBehavior>(SekCli.DesugarLet(new ParallelBehavior { Items = { Inv("A") } }));
    }

    [Fact]
    public void KindOf_MapsTypeNames()
    {
        Assert.Equal(ValueKind.String, SekCli.KindOf("string"));
        Assert.Equal(ValueKind.Bool, SekCli.KindOf("bool"));
        Assert.Equal(ValueKind.Long, SekCli.KindOf("long"));
        Assert.Equal(ValueKind.Long, SekCli.KindOf("ulong"));
        Assert.Equal(ValueKind.Int, SekCli.KindOf("int"));
        Assert.Equal(ValueKind.Int, SekCli.KindOf("byte"));
    }

    [Fact]
    public void Unwrap_PeelsNestedGroups()
    {
        var inner = Inv("A");
        var nested = new GroupBehavior { Inner = new GroupBehavior { Inner = inner } };
        Assert.Same(inner, SekCli.Unwrap(nested));
        Assert.Null(SekCli.Unwrap(null));
    }

    // ---- BuildGoalPredicate: property, field, method, missing member -------------------

    [Fact]
    public void BuildGoalPredicate_ReadsBoolMembers()
    {
        var intro = new ModelIntrospector(typeof(GoalModel));

        var byProp = SekCli.BuildGoalPredicate(intro, "Reached");
        Assert.NotNull(byProp);
        Assert.True(byProp!(new GoalModel { Reached = true }));
        Assert.False(byProp!(new GoalModel { Reached = false }));

        // dotted member name → trailing identifier is used
        var dotted = SekCli.BuildGoalPredicate(intro, "GoalModel.Reached");
        Assert.NotNull(dotted);

        // method predicate
        var byMethod = SekCli.BuildGoalPredicate(intro, "IsDone");
        Assert.NotNull(byMethod);
        Assert.True(byMethod!(new GoalModel { Reached = true }));

        // missing member → null; empty / inline → null
        Assert.Null(SekCli.BuildGoalPredicate(intro, "NoSuchMember"));
        Assert.Null(SekCli.BuildGoalPredicate(intro, "(inline)"));
        Assert.Null(SekCli.BuildGoalPredicate(intro, "  "));
    }

    // ---- AnalyzeCord: warning path and error path --------------------------------------

    [Fact]
    public void AnalyzeCord_UnknownBaseConfig_WarnsButSucceeds()
    {
        // SEM003: a config inheriting from an unknown base is a warning (printed), not an error.
        var cord = Cord("config C : Missing { }\nmachine M() : C { construct model program from C }\n");
        var model = SekCli.AnalyzeCord(cord, null);
        Assert.False(model.HasErrors);
    }

    [Fact]
    public void AnalyzeCord_DuplicateMachine_ThrowsOnError()
    {
        // SEM002: a machine declared twice is an error → AnalyzeCord surfaces it as an exception.
        var cord = Cord(
            "config C { }\n" +
            "machine M() : C { construct model program from C }\n" +
            "machine M() : C { construct model program from C }\n");
        var ex = Assert.Throws<InvalidOperationException>(() => SekCli.AnalyzeCord(cord, null));
        Assert.Contains("semantic analysis failed", ex.Message);
    }

    [Fact]
    public void AnalyzeCord_UnknownTargetMachine_ThrowsOnError()
    {
        var cord = Cord("config C { }\nmachine M() : C { construct model program from C }\n");
        Assert.Throws<InvalidOperationException>(() => SekCli.AnalyzeCord(cord, "NoSuchMachine"));
    }

    // ---- helpers -----------------------------------------------------------------------

    private static string Flatten(Behavior b) => string.Join(",", b.ReferencedTargets());

    private static string? ArgOf(Behavior b) => (b as InvocationBehavior)?.Args?.FirstOrDefault();

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
    }

    private static string MakeCordOnlyProject(bool withBinding)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sekcov_proj_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, ".specexplorerkit", "out"));
        Directory.CreateDirectory(Path.Combine(dir, "Model"));
        var binding = withBinding
            ? ",\n  \"binding\": { \"assembly\": \"Sut/bin/Debug/X.dll\", \"namespace\": \"X\" }"
            : string.Empty;
        File.WriteAllText(Path.Combine(dir, ".specexplorerkit", "config.json"),
            "{\n  \"model\": { \"assembly\": \"Model/bin/Debug/X.dll\", \"type\": \"X.Model\" },\n" +
            "  \"cord\": \"Model\"" + binding + ",\n  \"out\": \".specexplorerkit/out\"\n}");
        File.WriteAllText(Path.Combine(dir, "Model", "Config.cord"),
            "config C { }\nmachine ModelProgram() : C { construct model program from C }\n");
        return dir;
    }

    public sealed class GoalModel : ModelProgram
    {
        public bool Reached { get; set; }

        [Rule("GoalModel.Step")]
        public void Step() => Reached = true;

        public bool IsDone() => Reached;
    }
}
