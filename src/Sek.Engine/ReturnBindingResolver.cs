namespace Sek.Engine;

/// <summary>
/// Resolves Cord return-bindings (<c>Producer(args) / handle; … ; Consumer(handle)</c>): a
/// producer action's return value is bound to a variable, and a later consumer action that
/// references the variable as an argument uses the captured value.
///
/// <para>This is the dataflow layer for return-bindings. Given a linear trace of steps and the
/// concrete return value each producing step yielded, it computes the concrete argument each
/// consumer step should use, and reports whether an observed trace is consistent with the
/// bindings (a consumer's argument equals the value its producer returned).</para>
/// </summary>
public static class ReturnBindingResolver
{
    /// <summary>One step of a return-binding trace.</summary>
    /// <param name="Action">The action label.</param>
    /// <param name="Bind">Variable this step binds to its return value (from <c>/ var</c>), or null.</param>
    /// <param name="Args">Argument tokens; a token equal to a bound variable name is a reference.</param>
    /// <param name="Return">The value this step returned (null for void), used to fill its binding.</param>
    public readonly record struct Step(string Action, string? Bind, IReadOnlyList<string> Args, string? Return);

    /// <summary>
    /// Substitutes bound return values into each step's argument references, in order. A step binds
    /// its <see cref="Step.Bind"/> variable to its <see cref="Step.Return"/> only <em>after</em> its
    /// own arguments are resolved, so a variable is visible to later steps but not its own. A
    /// reference to an as-yet-unbound variable is left unchanged.
    /// </summary>
    public static List<List<string>> ResolveArguments(IReadOnlyList<Step> trace)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        var resolved = new List<List<string>>(trace.Count);
        foreach (var step in trace)
        {
            resolved.Add(step.Args.Select(a => env.TryGetValue(a, out var v) ? v : a).ToList());
            if (step.Bind is not null && step.Return is not null) env[step.Bind] = step.Return;
        }

        return resolved;
    }
}
