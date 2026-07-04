namespace Sek.Modeling;

/// <summary>
/// Familiar helper mirroring Spec Explorer's <c>Microsoft.Modeling.Condition</c>. A
/// false condition disables the current action (throws <see cref="GuardDisabledException"/>).
/// </summary>
public static class Condition
{
    public static void IsTrue(bool condition, string reason)
    {
        if (!condition)
        {
            throw new GuardDisabledException(reason);
        }
    }
}
