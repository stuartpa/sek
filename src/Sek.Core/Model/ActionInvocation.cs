namespace Sek.Core.Model;

/// <summary>
/// A concrete action occurrence that labels a transition: an action name plus the
/// concrete argument values it was invoked with. This is the modern equivalent of a
/// Spec Explorer "step" (a bound action invocation).
/// </summary>
/// <param name="Kind">Action kind — <c>call</c> (controllable stimulus, default),
/// <c>return</c>, or <c>event</c> (observable raised by the SUT).</param>
/// <param name="Result">The action's return value (stringified), when it returns one — the value
/// a Cord <c>Action(args) / var</c> return-binding captures. Null for <c>void</c> actions.</param>
public sealed record ActionInvocation(string Name, IReadOnlyList<string> Arguments, string Kind = "call", string? Result = null)
{
    public static ActionInvocation Of(string name, params string[] arguments) =>
        new(name, arguments);

    /// <summary>True for an uncontrollable observation (Cord <c>action event</c>).</summary>
    public bool IsEvent => string.Equals(Kind, "event", StringComparison.OrdinalIgnoreCase);

    /// <summary>Human-readable label, e.g. <c>Warehouse.CreateWarehouse(1)</c>.</summary>
    public string Display =>
        Arguments.Count == 0 ? Name : $"{Name}({string.Join(", ", Arguments)})";

    public override string ToString() => Display;
}
