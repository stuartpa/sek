using System.Text;
using Sek.Core.Model;

namespace Sek.Cli;

/// <summary>
/// Generates executable tests from an exploration graph. Test cases are witness paths
/// through the transition system — each starts at the initial state and (where possible)
/// ends at an accepting state — chosen to cover the model's transitions. Paths are then
/// emitted as an xUnit test project whose tests replay each action sequence against the
/// system-under-test binding (mirroring <see cref="Conformance"/>).
/// </summary>
public static class TestGen
{
    public sealed record TestPath(IReadOnlyList<Transition> Steps);

    public sealed record GenResult(int TestCount, int CoveredTransitions, int TotalTransitions, string ProjectDir, string TestFile);

    /// <summary>Cord test-generation strategy (<c>construct test cases where Strategy = "…"</c>).</summary>
    public enum TestStrategy
    {
        /// <summary>Many short tests: each covers one still-uncovered transition then ends at the
        /// nearest accepting state (shortest witnesses).</summary>
        Short,

        /// <summary>Few long tests: each greedily chains many uncovered transitions into a single
        /// covering tour before ending at an accepting state.</summary>
        Long,
    }

    /// <summary>Maps a Cord strategy name (<c>shorttests</c>/<c>longtests</c>) to a
    /// <see cref="TestStrategy"/>; unknown/empty names default to <see cref="TestStrategy.Long"/>.</summary>
    public static TestStrategy ParseStrategy(string? name) =>
        name?.Trim().ToLowerInvariant() switch
        {
            "shorttests" or "short" => TestStrategy.Short,
            _ => TestStrategy.Long,
        };

    /// <summary>
    /// Selects witness paths covering the graph's transitions (up to <paramref name="maxTests"/>
    /// paths, each at most <paramref name="maxSteps"/> long). With <see cref="TestStrategy.Long"/>
    /// each path greedily chains uncovered transitions into a long covering tour; with
    /// <see cref="TestStrategy.Short"/> each path covers a single uncovered transition and then
    /// routes to the nearest accepting state, yielding many short tests.
    /// </summary>
    public static List<TestPath> SelectPaths(ExplorationGraph graph, int maxTests, TestStrategy strategy = TestStrategy.Long, int maxSteps = 500)
    {
        var init = graph.InitialStateId ?? graph.States.FirstOrDefault(s => s.Initial)?.Id ?? "S0";
        var prefix = ShortestFromInit(graph, init);
        var suffix = ShortestToAccepting(graph);
        var outgoing = graph.Transitions
            .GroupBy(t => t.FromStateId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var covered = new HashSet<Transition>();
        var paths = new List<TestPath>();
        var total = graph.Transitions.Count;

        while (paths.Count < maxTests && covered.Count < total)
        {
            // Seed: the first still-uncovered transition reachable from the initial state.
            Transition? seed = null;
            foreach (var t in graph.Transitions)
            {
                if (covered.Contains(t)) continue;
                if (t.FromStateId == init || prefix.ContainsKey(t.FromStateId)) { seed = t; break; }
            }

            if (seed is null) break; // remaining uncovered transitions are unreachable

            var steps = new List<Transition>(PathFromInit(prefix, seed.FromStateId));
            var current = seed.FromStateId;

            if (strategy == TestStrategy.Short)
            {
                // Short test: cover exactly the seed transition, then stop at an accepting state.
                steps.Add(seed);
                covered.Add(seed);
                current = seed.ToStateId;
            }
            else
            {
                // Build a covering tour: take uncovered outgoing transitions; when the current
                // state has none, navigate (over already-covered edges) to the nearest state that
                // still has an uncovered outgoing transition, and continue — until the step budget
                // is reached or no uncovered transition is reachable.
                while (steps.Count < maxSteps)
                {
                    var nextT = outgoing.TryGetValue(current, out var outs)
                        ? outs.FirstOrDefault(t => !covered.Contains(t))
                        : null;

                    if (nextT is not null)
                    {
                        steps.Add(nextT);
                        covered.Add(nextT);
                        current = nextT.ToStateId;
                        continue;
                    }

                    var nav = NavigateToUncovered(graph, outgoing, current, covered, maxSteps - steps.Count);
                    if (nav is null || nav.Count == 0) break;
                    foreach (var t in nav)
                    {
                        steps.Add(t);
                        covered.Add(t);
                        current = t.ToStateId;
                    }
                }
            }

            // Route to the nearest accepting state so the test ends at a valid stopping point.
            steps.AddRange(PathToAccepting(suffix, current));

            foreach (var t in steps) covered.Add(t);
            paths.Add(new TestPath(steps));
        }

        return paths;
    }

    /// <summary>BFS from <paramref name="start"/> over existing edges to the nearest state that
    /// has an uncovered outgoing transition; returns the transitions to walk there (empty if
    /// <paramref name="start"/> already qualifies, null if none reachable within the budget).</summary>
    private static List<Transition>? NavigateToUncovered(
        ExplorationGraph graph, Dictionary<string, List<Transition>> outgoing,
        string start, HashSet<Transition> covered, int budget)
    {
        bool HasUncovered(string s) => outgoing.TryGetValue(s, out var o) && o.Any(t => !covered.Contains(t));

        var parent = new Dictionary<string, Transition>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            if (s != start && HasUncovered(s))
            {
                var stack = new Stack<Transition>();
                var cur = s;
                while (parent.TryGetValue(cur, out var t)) { stack.Push(t); cur = t.FromStateId; }
                var path = stack.ToList();
                return path.Count <= budget ? path : null;
            }

            if (!outgoing.TryGetValue(s, out var outs)) continue;
            foreach (var t in outs)
            {
                if (visited.Add(t.ToStateId)) { parent[t.ToStateId] = t; queue.Enqueue(t.ToStateId); }
            }
        }

        return null;
    }

    private static Dictionary<string, Transition> ShortestFromInit(ExplorationGraph graph, string init)
    {
        var parent = new Dictionary<string, Transition>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { init };
        var queue = new Queue<string>();
        queue.Enqueue(init);
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            foreach (var t in graph.OutgoingFrom(s))
            {
                if (visited.Add(t.ToStateId))
                {
                    parent[t.ToStateId] = t;
                    queue.Enqueue(t.ToStateId);
                }
            }
        }

        return parent;
    }

    private static List<Transition> PathFromInit(Dictionary<string, Transition> parent, string target)
    {
        var stack = new Stack<Transition>();
        var cur = target;
        while (parent.TryGetValue(cur, out var t))
        {
            stack.Push(t);
            cur = t.FromStateId;
        }

        return stack.ToList();
    }

    private static Dictionary<string, Transition> ShortestToAccepting(ExplorationGraph graph)
    {
        // Reverse BFS from all accepting states; next[s] = transition to take from s to move
        // one hop closer to the nearest accepting state.
        var incoming = new Dictionary<string, List<Transition>>(StringComparer.Ordinal);
        foreach (var t in graph.Transitions)
        {
            (incoming.TryGetValue(t.ToStateId, out var l) ? l : incoming[t.ToStateId] = new List<Transition>()).Add(t);
        }

        var next = new Dictionary<string, Transition>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        foreach (var s in graph.States.Where(s => s.Accepting))
        {
            visited.Add(s.Id);
            queue.Enqueue(s.Id);
        }

        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            if (!incoming.TryGetValue(s, out var preds)) continue;
            foreach (var t in preds)
            {
                if (visited.Add(t.FromStateId))
                {
                    next[t.FromStateId] = t; // from FromStateId, take t to get closer to accepting
                    queue.Enqueue(t.FromStateId);
                }
            }
        }

        return next;
    }

    private static List<Transition> PathToAccepting(Dictionary<string, Transition> next, string start)
    {
        var steps = new List<Transition>();
        var cur = start;
        var guard = 0;
        while (next.TryGetValue(cur, out var t) && guard++ < 10000)
        {
            steps.Add(t);
            cur = t.ToStateId;
        }

        return steps;
    }

    /// <summary>Emits an xUnit test project (csproj + cs) into <paramref name="outDir"/>.</summary>
    public static GenResult EmitXunit(
        ExplorationGraph graph, List<TestPath> paths, string outDir,
        string testNamespace, string bindingAssemblyPath, string bindingNamespace)
    {
        Directory.CreateDirectory(outDir);
        var safe = Sanitize(graph.Machine);
        var className = safe + "Tests";
        var csproj = Path.Combine(outDir, className + ".csproj");
        var csfile = Path.Combine(outDir, className + ".cs");

        File.WriteAllText(csproj, Csproj());
        File.WriteAllText(csfile, TestSource(graph, paths, safe, className, testNamespace, bindingAssemblyPath, bindingNamespace));

        var covered = new HashSet<Transition>();
        foreach (var p in paths) foreach (var t in p.Steps) covered.Add(t);
        return new GenResult(paths.Count, covered.Count, graph.Transitions.Count, outDir, csfile);
    }

    private static string Csproj() =>
        """
        <Project Sdk="Microsoft.NET.Sdk">

          <!-- Auto-generated by `sek generate`. Run with: dotnet test -->
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>disable</Nullable>
            <ImplicitUsings>disable</ImplicitUsings>
            <IsPackable>false</IsPackable>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
            <PackageReference Include="xunit" Version="2.9.2" />
            <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
          </ItemGroup>

        </Project>

        """;

    private static string TestSource(
        ExplorationGraph graph, List<TestPath> paths, string safe, string className,
        string ns, string bindingPath, string bindingNs)
    {
        var covered = new HashSet<Transition>();
        foreach (var p in paths) foreach (var t in p.Steps) covered.Add(t);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine($"//   Generated by `sek generate` from machine '{graph.Machine}'.");
        sb.AppendLine($"//   {paths.Count} test(s) covering {covered.Count}/{graph.Transitions.Count} transitions.");
        sb.AppendLine("//   Each test replays an explored action sequence against the SUT binding.");
        sb.AppendLine("//   The binding assembly path is baked in below; override with the SEK_BINDING");
        sb.AppendLine("//   environment variable. Regenerate with: sek generate " + graph.Machine);
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable disable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using System.Runtime.Loader;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns}");
        sb.AppendLine("{");
        sb.AppendLine($"    public sealed class {className}");
        sb.AppendLine("    {");
        sb.AppendLine($"        private const string DefaultBinding = {Literal(bindingPath)};");
        sb.AppendLine($"        private const string BindingNamespace = {Literal(bindingNs)};");
        sb.AppendLine();
        sb.AppendLine("        private readonly Sut _sut = new Sut(");
        sb.AppendLine("            Environment.GetEnvironmentVariable(\"SEK_BINDING\") ?? DefaultBinding, BindingNamespace);");
        sb.AppendLine();

        for (var i = 0; i < paths.Count; i++)
        {
            var method = $"{safe}_Path{(i + 1):D2}";
            sb.AppendLine("        [Fact]");
            sb.AppendLine($"        public void {method}()");
            sb.AppendLine("        {");
            foreach (var t in paths[i].Steps)
            {
                var args = t.Action.Arguments.Count == 0
                    ? string.Empty
                    : ", " + string.Join(", ", t.Action.Arguments.Select(Literal));
                sb.AppendLine($"            _sut.Step({Literal(t.Action.Name)}{args});");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.Append(Harness());
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Harness() =>
        """
                private sealed class Sut
                {
                    private readonly Assembly _asm;
                    private readonly string _ns;

                    public Sut(string path, string ns)
                    {
                        var full = Path.GetFullPath(path);
                        if (!File.Exists(full))
                            throw new FileNotFoundException(
                                "SUT binding assembly not found: " + full +
                                ". Build the adapter or set the SEK_BINDING environment variable.");
                        var dir = Path.GetDirectoryName(full);
                        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
                        {
                            var candidate = Path.Combine(dir, name.Name + ".dll");
                            return File.Exists(candidate) ? ctx.LoadFromAssemblyPath(candidate) : null;
                        };
                        _asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(full);
                        _ns = ns;
                    }

                    public void Step(string label, params string[] args)
                    {
                        var dot = label.LastIndexOf('.');
                        Assert.True(dot > 0, "malformed action label: " + label);
                        var type = _asm.GetType(_ns + "." + label.Substring(0, dot));
                        Assert.True(type != null, "no SUT type for action " + label);
                        var method = type.GetMethod(label.Substring(dot + 1),
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                        Assert.True(method != null, "no SUT method for action " + label);
                        var ps = method.GetParameters();
                        var call = new object[ps.Length];
                        for (var i = 0; i < ps.Length && i < args.Length; i++)
                            call[i] = Coerce(args[i], ps[i].ParameterType);
                        var target = method.IsStatic ? null : Activator.CreateInstance(type);
                        method.Invoke(target, call);
                    }

                    private static object Coerce(string v, Type t)
                    {
                        if (t == typeof(string)) return v;
                        if (t.IsEnum) return Enum.Parse(t, v, true);
                        try { return Convert.ChangeType(v, t); } catch { return v; }
                    }
                }

        """;

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        var r = sb.ToString();
        return string.IsNullOrEmpty(r) || char.IsDigit(r[0]) ? "M" + r : r;
    }

    private static string Literal(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in s)
        {
            sb.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString(),
            });
        }

        sb.Append('"');
        return sb.ToString();
    }
}
