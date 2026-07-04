namespace Sek.Modeling;

/// <summary>
/// Thrown by <see cref="ModelProgram.Require"/> / <see cref="Condition.IsTrue"/> when a
/// rule's guard is not satisfied. The exploration engine catches this and treats the
/// action as simply <em>disabled</em> in the current state (not as an error).
/// </summary>
public sealed class GuardDisabledException : Exception
{
    public GuardDisabledException(string reason)
        : base(reason)
    {
    }
}
