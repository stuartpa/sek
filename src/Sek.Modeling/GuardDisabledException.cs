namespace Sek.Modeling;

/// <summary>
/// Thrown by <see cref="ModelProgram.Require"/> / <see cref="Condition.IsTrue"/> when a
/// rule's guard is not satisfied. The exploration engine catches this and treats the
/// action as simply <em>disabled</em> in the current state (not as an error).
/// </summary>
public sealed class GuardDisabledException : Exception
{
    /// <summary>True when the guard is an exploration bound (a limiter to keep the state space
    /// finite) rather than a real precondition. Bounds disable the action during exploration but
    /// are NOT recorded as model-derived negative edges (a conforming SUT need not reject them).</summary>
    public bool IsExplorationBound { get; }

    public GuardDisabledException(string reason, bool isExplorationBound = false)
        : base(reason)
    {
        IsExplorationBound = isExplorationBound;
    }
}
