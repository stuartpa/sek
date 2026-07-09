using System.Reflection;
using System.Runtime.Loader;
using Sek.Core.Model;

namespace Sek.Cli;

public sealed class ConformanceReport
{
    public int TransitionsReplayed { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }

    /// <summary>Model-derived negative edges replayed (illegal actions attempted).</summary>
    public int NegativeReplayed { get; set; }

    /// <summary>Illegal actions the SUT correctly rejected.</summary>
    public int NegativeRejected { get; set; }

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

        // Optional per-path reset: a stateful SUT (e.g. one backed by a database) can expose a
        // `public static void ResetForConformance()` on any type in the binding assembly. It is
        // called before each witness path and each negative test so every path starts from the
        // model's initial state, matching the offline exploration's per-path semantics.
        var reset = FindResetHook(asm);

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
                reset?.Invoke(null, null); // start each path from the model's initial state
                var instances = new Dictionary<Type, object?>();
                foreach (var t in path.Steps)
                {
                    report.TransitionsReplayed++;
                    var outcome = InvokeStep(asm, ns, t.Action, instances, out var msg);
                    if (outcome == StepOutcome.Ok)
                    {
                        report.Succeeded++;
                        report.ActionsCovered.Add(t.Action.Name);
                    }
                    else
                    {
                        report.Failed++;
                        report.Failures.Add(outcome == StepOutcome.Rejected ? $"{t.Action.Name}: {msg}" : msg);
                        break; // stop this path: the SUT state no longer matches the model
                    }
                }
            }

            // NEGATIVE conformance (model-derived): for each illegal (state, action) pair, drive the
            // legal prefix to that state then attempt the action — a conforming SUT must REJECT it.
            // An illegal action the SUT accepts is a conformance failure.
            foreach (var neg in graph.NegativeTransitions)
            {
                reset?.Invoke(null, null); // clean state before driving the negative's legal prefix
                var instances = new Dictionary<Type, object?>();
                var prefixOk = true;
                foreach (var t in TestGen.LegalPrefixTo(graph, neg.FromStateId))
                {
                    if (InvokeStep(asm, ns, t.Action, instances, out var pmsg) != StepOutcome.Ok)
                    {
                        report.Failed++;
                        report.Failures.Add($"negative setup: could not reach {neg.FromStateId} to test '{neg.Action.Name}': {pmsg}");
                        prefixOk = false;
                        break;
                    }
                }

                if (!prefixOk) continue;

                report.NegativeReplayed++;
                var negOutcome = InvokeStep(asm, ns, neg.Action, instances, out var nmsg);
                if (negOutcome == StepOutcome.Rejected)
                {
                    report.NegativeRejected++;
                    report.ActionsCovered.Add(neg.Action.Name);
                }
                else if (negOutcome == StepOutcome.Ok)
                {
                    report.Failed++;
                    report.Failures.Add($"illegal action '{neg.Action.Name}' was ACCEPTED at {neg.FromStateId} — the model forbids it ({neg.Reason}).");
                }
                else
                {
                    report.Failed++;
                    report.Failures.Add($"negative '{neg.Action.Name}': {nmsg}");
                }
            }
        }
        finally
        {
            Console.SetOut(savedOut);
        }

        return report;
    }

    /// <summary>Finds an optional per-path reset hook in the binding: the first accessible
    /// <c>public static void ResetForConformance()</c> declared on any type. Returns null when
    /// the SUT is stateless (no reset needed).</summary>
    private static MethodInfo? FindResetHook(Assembly asm)
    {
        foreach (var type in SafeGetTypes(asm))
        {
            var method = type.GetMethod(
                "ResetForConformance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            if (method is not null && method.ReturnType == typeof(void))
            {
                return method;
            }
        }

        return null;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }

    private enum StepOutcome { Ok, Rejected, Malformed, NoMethod }

    /// <summary>Invokes one action against the SUT (reusing per-type instances). <see
    /// cref="StepOutcome.Ok"/> = accepted (ran); <see cref="StepOutcome.Rejected"/> = the SUT threw
    /// (a rejection — the expected outcome for a negative edge); the others are setup problems.</summary>
    private static StepOutcome InvokeStep(Assembly asm, string ns, ActionInvocation action, Dictionary<Type, object?> instances, out string message)
    {
        message = string.Empty;
        var label = action.Name;
        var dot = label.LastIndexOf('.');
        if (dot <= 0)
        {
            message = $"malformed action label '{label}'";
            return StepOutcome.Malformed;
        }

        var typeName = $"{ns}.{label[..dot]}";
        var methodName = label[(dot + 1)..];
        var type = asm.GetType(typeName);
        var method = type?.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        if (method is null)
        {
            message = $"no SUT method for action '{label}' (looked for {typeName}.{methodName})";
            return StepOutcome.NoMethod;
        }

        try
        {
            var ps = method.GetParameters();
            var args = new object?[ps.Length];
            for (var k = 0; k < ps.Length && k < action.Arguments.Count; k++)
            {
                args[k] = Coerce(action.Arguments[k], ps[k].ParameterType);
            }

            var target = method.IsStatic ? null : Instance(instances, type!);
            method.Invoke(target, args);
            return StepOutcome.Ok;
        }
        catch (Exception ex)
        {
            message = ex.InnerException?.Message ?? ex.Message;
            return StepOutcome.Rejected;
        }
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
