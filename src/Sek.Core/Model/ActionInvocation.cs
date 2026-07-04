namespace Sek.Core.Model;

/// <summary>
/// A concrete action occurrence that labels a transition: an action name plus the
/// concrete argument values it was invoked with. This is the modern equivalent of a
/// Spec Explorer "step" (a bound action invocation).
/// </summary>
public sealed record ActionInvocation(string Name, IReadOnlyList<string> Arguments)
{
    public static ActionInvocation Of(string name, params string[] arguments) =>
        new(name, arguments);

    /// <summary>Human-readable label, e.g. <c>Warehouse.CreateWarehouse(1)</c>.</summary>
    public string Display =>
        Arguments.Count == 0 ? Name : $"{Name}({string.Join(", ", Arguments)})";

    public override string ToString() => Display;
}
