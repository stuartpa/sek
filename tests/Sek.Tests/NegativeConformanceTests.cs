using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sek.Cli;
using Sek.Cord;
using Sek.Cord.Ast;
using Sek.Core.Model;
using Sek.Core.Seexpl;
using Sek.Engine;
using Sek.Modeling;
using Xunit;

// ---- SUTs loaded by reflection from this test assembly ------------------------------------

namespace Sek.Tests.GateSut
{
    /// <summary>A conforming SUT: <c>Use</c> is rejected (throws) until <c>Open</c>.</summary>
    public sealed class W
    {
        public bool Opened;
        public void Open() => Opened = true;
        public void Use() { if (!Opened) throw new InvalidOperationException("use requires open"); }
    }
}

namespace Sek.Tests.GateSutBad
{
    /// <summary>A NON-conforming SUT: <c>Use</c> is accepted even when it should be rejected.</summary>
    public sealed class W
    {
        public bool Opened;
        public void Open() => Opened = true;
        public void Use() { /* wrongly accepts a model-forbidden action */ }
    }
}

namespace Sek.Tests
{
    /// <summary>
    /// Coverage for model-derived <b>negative conformance</b> (IN003 / EngLoopKit PM004): the
    /// explorer records guard-disabled (illegal) actions as <see cref="NegativeTransition"/>s; the
    /// <c>.seexpl</c> round-trips them; <see cref="TestGen"/> emits rejection tests; and
    /// <see cref="Conformance"/> replays them — passing when the SUT refuses an illegal action and
    /// failing when it wrongly accepts one.
    /// </summary>
    public class NegativeConformanceTests
    {
        private static readonly string ThisAssembly = typeof(NegativeConformanceTests).Assembly.Location;

        /// <summary>A tiny guarded model: <c>Use</c> is legal only after <c>Open</c>.</summary>
        public sealed class GateModel : ModelProgram
        {
            public bool Opened { get; set; }

            [Rule("W.Open")]
            public void Open() => Opened = true;

            [Rule("W.Use")]
            public void Use() => Require(Opened, "use requires open");

            [AcceptingCondition]
            public bool Done() => true;
        }

        private static ExplorationGraph ExploreGate()
        {
            var intro = new ModelIntrospector(typeof(GateModel));
            var cord = CordDocument.ParseText("config C { action all W; }\nmachine M() : C { construct model program from C }\n");
            var cb = new ConstructBehavior { Kind = ConstructKind.ModelProgram, Reference = "C" };
            var options = new ExplorationOptions { MaxStates = 8, MaxTransitions = 16, MaxDepth = 6 };
            var binds = new Dictionary<string, List<List<string>>>(StringComparer.Ordinal);
            return SekCli.InterpretConstruct(intro, cord, "M", cb, options, "enum", binds).Graph;
        }

        [Fact]
        public void Explorer_RecordsGuardDisabledActionsAsNegativeEdges()
        {
            var g = ExploreGate();
            var neg = Assert.Single(g.NegativeTransitions);          // Use is illegal in the initial (unopened) state
            Assert.Equal("W.Use", neg.Action.Name);
            Assert.Equal(g.InitialStateId, neg.FromStateId);
            Assert.Contains("open", neg.Reason);
        }

        [Fact]
        public void Seexpl_RoundTripsNegativeTransitions()
        {
            var g = ExploreGate();
            var path = Path.Combine(Path.GetTempPath(), "neg_" + Guid.NewGuid().ToString("N") + ".seexpl");
            try
            {
                SeexplDocument.FromGraph(g).Save(path);
                var back = SeexplDocument.Load(path).ToGraph();
                var neg = Assert.Single(back.NegativeTransitions);
                Assert.Equal("W.Use", neg.Action.Name);
                Assert.Contains("open", neg.Reason);
            }
            finally { try { File.Delete(path); } catch { } }
        }

        [Fact]
        public void TestGen_EmitsRejectionTests_And_LegalPrefix()
        {
            var g = ExploreGate();
            var paths = TestGen.SelectPaths(g, 5, TestGen.TestStrategy.Long);
            var outDir = Path.Combine(Path.GetTempPath(), "neg_emit_" + Guid.NewGuid().ToString("N"));
            try
            {
                var res = TestGen.EmitXunit(g, paths, outDir, "Gen.Tests", ThisAssembly, "Sek.Tests.GateSut");
                Assert.Equal(1, res.NegativeTestCount);
                var src = File.ReadAllText(res.TestFile);
                Assert.Contains("StepExpectingError(\"W.Use\"", src);   // model-derived rejection test
                Assert.Contains("Reject_W_Use", src);
            }
            finally { try { Directory.Delete(outDir, true); } catch { } }
        }

        [Fact]
        public void LegalPrefixTo_InitialState_IsEmpty()
        {
            var g = ExploreGate();
            Assert.Empty(TestGen.LegalPrefixTo(g, g.InitialStateId!));
        }

        [Fact]
        public void Conformance_ConformingSut_RejectsIllegalAction()
        {
            var g = ExploreGate();
            var report = Conformance.Replay(g, ThisAssembly, "Sek.Tests.GateSut");
            Assert.True(report.Passed, string.Join("; ", report.Failures));
            Assert.Equal(1, report.NegativeReplayed);
            Assert.Equal(1, report.NegativeRejected);     // the SUT correctly refused Use-before-Open
        }

        [Fact]
        public void Conformance_NonConformingSut_AcceptingIllegalAction_Fails()
        {
            var g = ExploreGate();
            var report = Conformance.Replay(g, ThisAssembly, "Sek.Tests.GateSutBad");
            Assert.False(report.Passed);                  // the SUT wrongly accepted a model-forbidden action
            Assert.Contains(report.Failures, f => f.Contains("was ACCEPTED"));
        }
    }
}
