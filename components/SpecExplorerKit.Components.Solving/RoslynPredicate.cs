using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace SpecExplorerKit.Components.Solving;

/// <summary>
/// Globals object handed to a compiled predicate script. Exposes the current parameter
/// assignment via <see cref="V"/>; the generated preamble copies these into strongly
/// typed locals before evaluating the user's expression.
/// </summary>
public sealed class PredicateGlobals
{
    public IReadOnlyDictionary<string, object?> V { get; init; } =
        new Dictionary<string, object?>();
}

/// <summary>
/// Compiles arbitrary embedded C# boolean expressions (the body of a
/// <c>Condition.IsTrue(...)</c>) into a reusable delegate using Roslyn scripting. This is
/// SEK's execution path for embedded C# that the lightweight <c>Expr</c> tree cannot
/// represent (method calls, <c>Math.*</c>, string members, ternaries, casts, ...).
///
/// Each distinct (expression, parameter-signature) pair is compiled once and cached; the
/// resulting <see cref="ScriptRunner{Boolean}"/> executes compiled IL per assignment.
/// </summary>
public static class RoslynPredicate
{
    private static readonly ConcurrentDictionary<string, ScriptRunner<bool>?> Cache =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Attempts to compile <paramref name="expression"/> (a C# boolean expression that may
    /// reference the given parameters by name) into a predicate over an assignment. Returns
    /// <c>null</c> if the expression cannot be compiled (e.g. references unknown symbols).
    /// </summary>
    public static Func<IReadOnlyDictionary<string, object?>, bool>? TryCompile(
        string expression, IReadOnlyList<SolverParam> parameters)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var key = Signature(parameters) + "\u0001" + expression.Trim();
        var runner = Cache.GetOrAdd(key, _ => Build(expression.Trim(), parameters));
        if (runner is null)
        {
            return null;
        }

        return assignment =>
        {
            var globals = new PredicateGlobals { V = assignment };
            try
            {
                return runner(globals).GetAwaiter().GetResult();
            }
            catch
            {
                // A runtime error (bad cast, null deref, ...) means the assignment does not
                // satisfy the predicate rather than a hard failure.
                return false;
            }
        };
    }

    private static ScriptRunner<bool>? Build(string expression, IReadOnlyList<SolverParam> parameters)
    {
        try
        {
            var preamble = new System.Text.StringBuilder();
            foreach (var p in parameters)
            {
                var name = p.Name;
                var accessor = $"V.ContainsKey(\"{name}\") ? V[\"{name}\"] : null";
                switch (p.Kind)
                {
                    case ValueKind.Bool:
                        preamble.Append($"bool {name} = System.Convert.ToBoolean(({accessor}) ?? false);\n");
                        break;
                    case ValueKind.Long:
                        preamble.Append($"long {name} = System.Convert.ToInt64(({accessor}) ?? 0L);\n");
                        break;
                    case ValueKind.String:
                        preamble.Append($"string {name} = ({accessor})?.ToString();\n");
                        break;
                    default: // Int / enum-as-int
                        preamble.Append($"int {name} = System.Convert.ToInt32(({accessor}) ?? 0);\n");
                        break;
                }
            }

            var code = preamble.ToString() + "return (" + expression + ");";
            var options = ScriptOptions.Default
                .WithImports("System", "System.Linq", "System.Collections.Generic")
                .WithReferences(typeof(object).Assembly, typeof(Enumerable).Assembly);

            var script = CSharpScript.Create<bool>(code, options, typeof(PredicateGlobals));
            script.Compile();
            return script.CreateDelegate();
        }
        catch
        {
            return null;
        }
    }

    private static string Signature(IReadOnlyList<SolverParam> parameters) =>
        string.Join(",", parameters.Select(p => p.Name + ":" + p.Kind));
}
