namespace Sek.Modeling;

/// <summary>
/// Marks a parameterless <c>bool</c>-returning method as an accepting-state condition.
/// A state is accepting when all such methods return true. Accepting states are the
/// goal states for "construct accepting paths" and "construct test cases".
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AcceptingConditionAttribute : Attribute
{
}
