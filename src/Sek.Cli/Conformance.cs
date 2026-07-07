using System.Reflection;
using System.Runtime.Loader;
using Sek.Core.Model;

namespace Sek.Cli;

public sealed class ConformanceReport
{
    public int TransitionsReplayed { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public HashSet<string> ActionsCovered { get; } = new();
    public List<string> Failures { get; } = new();
    public bool Passed => Failed == 0;
}

/// <summary>
/// Replays an exploration graph's transitions against the system under test (SUT) via
/// its binding assembly. Each action label <c>X.Y</c> maps to the static method
/// <c>{namespace}.X.Y(args)</c> (the Adapter shims), exercising the implementation
/// (e.g. FakeImpl). This is offline conformance: every explored action must be callable
/// and must execute without error.
/// </summary>
public static class Conformance
{
    public static ConformanceReport Replay(ExplorationGraph graph, string bindingAssemblyPath, string ns)
    {
        var full = Path.GetFullPath(bindingAssemblyPath);
        if (!File.Exists(full))
        {
            throw new FileNotFoundException($"Binding assembly not found: {full}. Build the Adapter project first.");
        }

        ModelLoader.InstallProbingResolver(Path.GetDirectoryName(full)!);
        var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(full);

        var report = new ConformanceReport();

        // Replay witness PATHS (init → accepting), not isolated transitions: a stateful SUT must be
        // driven to a transition's source state before that transition is exercised. Each path uses
        // ONE SUT instance (per type), reused across the path's steps — mirroring the generated
        // harness (see IN001). Replaying transitions in isolation on a fresh instance would spuriously
        // fail any guarded action (e.g. a `View` that requires a prior `Explore`).
        var paths = TestGen.SelectPaths(graph, Math.Max(1, graph.Transitions.Count), TestGen.TestStrategy.Long);

        // Silence the SUT's console chatter (FakeImpl writes a line per call).
        var savedOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            foreach (var path in paths)
            {
                var instances = new Dictionary<Type, object?>();
                foreach (var t in path.Steps)
                {
                    report.TransitionsReplayed++;
                    var label = t.Action.Name;
                    var dot = label.LastIndexOf('.');
                    if (dot <= 0)
                    {
                        report.Failed++;
                        report.Failures.Add($"malformed action label '{label}'");
                        break; // path state is now indeterminate
                    }

                    var typeName = $"{ns}.{label[..dot]}";
                    var methodName = label[(dot + 1)..];
                    var type = asm.GetType(typeName);
                    var method = type?.GetMethod(
                        methodName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

                    if (method is null)
                    {
                        report.Failed++;
                        report.Failures.Add($"no SUT method for action '{label}' (looked for {typeName}.{methodName})");
                        break;
                    }

                    try
                    {
                        var ps = method.GetParameters();
                        var args = new object?[ps.Length];
                        for (var k = 0; k < ps.Length && k < t.Action.Arguments.Count; k++)
                        {
                            args[k] = Coerce(t.Action.Arguments[k], ps[k].ParameterType);
                        }

                        var target = method.IsStatic ? null : Instance(instances, type!);
                        method.Invoke(target, args);
                        report.Succeeded++;
                        report.ActionsCovered.Add(label);
                    }
                    catch (Exception ex)
                    {
                        report.Failed++;
                        report.Failures.Add($"{label}: {ex.InnerException?.Message ?? ex.Message}");
                        break; // stop this path: the SUT state no longer matches the model
                    }
                }
            }
        }
        finally
        {
            Console.SetOut(savedOut);
        }

        return report;
    }

    /// <summary>One SUT instance per type, reused across a path's steps (get-or-create).</summary>
    private static object? Instance(Dictionary<Type, object?> instances, Type type)
    {
        if (!instances.TryGetValue(type, out var obj))
        {
            obj = Activator.CreateInstance(type);
            instances[type] = obj;
        }

        return obj;
    }

    private static object? Coerce(string value, Type target)
    {
        if (target == typeof(string))
        {
            return value;
        }

        try
        {
            return Convert.ChangeType(value, target);
        }
        catch
        {
            return value;
        }
    }
}
