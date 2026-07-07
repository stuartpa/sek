using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sek.Cli;
using Sek.Core.Model;
using Sek.Modeling;
using Xunit;

// ---- test SUT + model types (loaded by reflection from this very test assembly) ----------

namespace Sek.Tests.ConfSut
{
    /// <summary>An instance SUT with argument-taking and throwing methods, so a conformance replay
    /// exercises the per-instance reuse, argument coercion, and failure paths.</summary>
    public sealed class Widget
    {
        public int State;
        public void Do(int n, string s) => State += n;
        public void Ping() { }
        public void Boom() => throw new InvalidOperationException("boom");
    }
}

namespace Sek.Tests.SingleScope { public sealed class Only : ModelProgram { } }
namespace Sek.Tests.MultiScope { public sealed class A : ModelProgram { } public sealed class B : ModelProgram { } }

namespace Sek.Tests
{
    /// <summary>
    /// Branch coverage for <see cref="Conformance"/> (argument coercion, per-instance reuse, failure and
    /// missing-method paths), <see cref="ModelLoader"/> (scope resolution + error paths), and
    /// <see cref="TestGen"/> (short/long path selection + xUnit emission with args and events).
    /// </summary>
    public class ConformanceCoverageTests
    {
        private static readonly string ThisAssembly = typeof(ConformanceCoverageTests).Assembly.Location;

        private static ExplorationGraph Graph(params (string from, ActionInvocation action, string to, bool accepting)[] edges)
        {
            var g = new ExplorationGraph { Machine = "M", InitialStateId = "S0" };
            var ids = new HashSet<string>();
            void AddState(string id, bool accepting, bool initial)
            {
                if (ids.Add(id)) g.States.Add(new ModelState(id, "h" + id, Accepting: accepting, Initial: initial));
            }
            AddState("S0", edges.Length == 0, initial: true);
            foreach (var (from, action, to, accepting) in edges)
            {
                AddState(from, false, false);
                AddState(to, accepting, false);
                g.Transitions.Add(new Transition(from, action, to));
            }
            return g;
        }

        // ---- Conformance -------------------------------------------------------------------

        [Fact]
        public void Conformance_ArgsInstanceReuse_Succeeds()
        {
            // One path, two steps on the same Widget instance: Do(3,"hi") then Ping().
            var g = Graph(
                ("S0", ActionInvocation.Of("Widget.Do", "3", "hi"), "S1", false),
                ("S1", ActionInvocation.Of("Widget.Ping"), "S2", true));
            var report = Conformance.Replay(g, ThisAssembly, "Sek.Tests.ConfSut");
            Assert.True(report.Passed, string.Join("; ", report.Failures));
            Assert.Equal(2, report.Succeeded);
            Assert.Contains("Widget.Do", report.ActionsCovered);
        }

        [Fact]
        public void Conformance_CoerceUnparseableArg_StillDrivesThenReports()
        {
            // "notanint" can't convert to int → Coerce falls back to the raw string, the invoke throws,
            // and the failure is reported (exercises both the Coerce catch and the invoke-exception path).
            var g = Graph(("S0", ActionInvocation.Of("Widget.Do", "notanint", "x"), "S1", true));
            var report = Conformance.Replay(g, ThisAssembly, "Sek.Tests.ConfSut");
            Assert.False(report.Passed);
            Assert.NotEmpty(report.Failures);
        }

        [Fact]
        public void Conformance_ThrowingMethod_IsReported()
        {
            var g = Graph(("S0", ActionInvocation.Of("Widget.Boom"), "S1", true));
            var report = Conformance.Replay(g, ThisAssembly, "Sek.Tests.ConfSut");
            Assert.False(report.Passed);
            Assert.Contains(report.Failures, m => m.Contains("boom"));
        }

        [Fact]
        public void Conformance_NoSutMethod_IsReported()
        {
            var g = Graph(("S0", ActionInvocation.Of("Widget.Missing"), "S1", true));
            var report = Conformance.Replay(g, ThisAssembly, "Sek.Tests.ConfSut");
            Assert.False(report.Passed);
            Assert.Contains(report.Failures, m => m.Contains("no SUT method"));
        }

        [Fact]
        public void Conformance_MissingAssembly_Throws() =>
            Assert.Throws<FileNotFoundException>(() =>
                Conformance.Replay(Graph(("S0", ActionInvocation.Of("Widget.Ping"), "S1", true)),
                    Path.Combine(Path.GetTempPath(), "no_such_binding_" + Guid.NewGuid().ToString("N") + ".dll"),
                    "Sek.Tests.ConfSut"));

        // ---- ModelLoader -------------------------------------------------------------------

        [Fact]
        public void ModelLoader_LoadModelType_MissingAssembly_Throws() =>
            Assert.Throws<FileNotFoundException>(() =>
                ModelLoader.LoadModelType(Path.Combine(Path.GetTempPath(), "nope_" + Guid.NewGuid().ToString("N") + ".dll"), "X"));

        [Fact]
        public void ModelLoader_LoadModelType_TypeNotFound_Throws() =>
            Assert.Throws<InvalidOperationException>(() =>
                ModelLoader.LoadModelType(ThisAssembly, "Sek.Tests.NoSuchType"));

        [Fact]
        public void ModelLoader_InScope_SingleCandidate_Resolves()
        {
            var t = ModelLoader.LoadModelTypeInScope(ThisAssembly, "Sek.Tests.SingleScope", "unused");
            Assert.Equal("Sek.Tests.SingleScope.Only", t.FullName);
        }

        [Fact]
        public void ModelLoader_InScope_EmptyScope_FallsBackToDefault()
        {
            var t = ModelLoader.LoadModelTypeInScope(ThisAssembly, "  ", "Sek.Tests.SingleScope.Only");
            Assert.Equal("Sek.Tests.SingleScope.Only", t.FullName);
        }

        [Fact]
        public void ModelLoader_InScope_MultipleCandidates_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ModelLoader.LoadModelTypeInScope(ThisAssembly, "Sek.Tests.MultiScope", "unused"));
            Assert.Contains("matches", ex.Message);
        }

        [Fact]
        public void ModelLoader_InScope_NoCandidate_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ModelLoader.LoadModelTypeInScope(ThisAssembly, "Sek.Tests.NoSuchScope", "unused"));
            Assert.Contains("No model program", ex.Message);
        }

        [Fact]
        public void ModelLoader_InScope_MissingAssembly_Throws() =>
            Assert.Throws<FileNotFoundException>(() =>
                ModelLoader.LoadModelTypeInScope(
                    Path.Combine(Path.GetTempPath(), "nope_" + Guid.NewGuid().ToString("N") + ".dll"),
                    "Sek.Tests.SingleScope", "x"));

        // ---- TestGen -----------------------------------------------------------------------

        [Fact]
        public void TestGen_Short_CoversEachTransitionSeparately()
        {
            var g = Graph(
                ("S0", ActionInvocation.Of("T.a"), "S1", true),
                ("S1", ActionInvocation.Of("T.b"), "S2", true),
                ("S0", ActionInvocation.Of("T.c"), "S2", true));
            var paths = TestGen.SelectPaths(g, maxTests: 10, strategy: TestGen.TestStrategy.Short);
            Assert.NotEmpty(paths);
            Assert.All(paths, p => Assert.NotEmpty(p.Steps));
        }

        [Fact]
        public void TestGen_Long_ToursAndNavigatesOverCoveredEdges()
        {
            // Shaped so the Long tour exhausts A's uncovered edges (going A→B→A over the cycle),
            // gets stuck at A with only covered outgoing, then must navigate back to B (over the
            // covered A→B edge) to reach B's still-uncovered edge to the accepting state. This
            // exercises NavigateToUncovered returning a real path. Edge order matters.
            var g = Graph(
                ("S0", ActionInvocation.Of("T.a"), "A", false),
                ("A", ActionInvocation.Of("T.toB"), "B", false),
                ("B", ActionInvocation.Of("T.toA"), "A", false),
                ("B", ActionInvocation.Of("T.u"), "C", true));
            var paths = TestGen.SelectPaths(g, maxTests: 5, strategy: TestGen.TestStrategy.Long);
            Assert.NotEmpty(paths);
            // All four transitions get covered.
            var covered = paths.SelectMany(p => p.Steps).Select(t => t.Action.Name).Distinct().ToList();
            Assert.Contains("T.u", covered);
            Assert.Contains("T.toA", covered);
        }

        [Fact]
        public void TestGen_EmitXunit_EmitsArgsAndEvents()
        {
            var g = Graph(
                ("S0", ActionInvocation.Of("Widget.Do", "3", "hi"), "S1", false),
                ("S1", new ActionInvocation("Widget.Ping", new List<string>(), "event"), "S2", true));
            var paths = TestGen.SelectPaths(g, 5, TestGen.TestStrategy.Long);
            var outDir = Path.Combine(Path.GetTempPath(), "sekcov_emit_" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = TestGen.EmitXunit(g, paths, outDir, "Gen.Tests", ThisAssembly, "Sek.Tests.ConfSut");
                var src = File.ReadAllText(result.TestFile);
                Assert.Contains("_sut.Step(\"Widget.Do\", \"3\", \"hi\")", src);
                Assert.Contains("_sut.Observe(\"Widget.Ping\")", src);
            }
            finally
            {
                try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { /* best effort */ }
            }
        }
    }
}
