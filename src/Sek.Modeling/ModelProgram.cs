namespace Sek.Modeling;

/// <summary>
/// Base class for SpecExplorerKit model programs.
///
/// Author a model by deriving from this class, holding all model state in public
/// read/write properties (so the engine can snapshot it), exposing a parameterless
/// constructor, and writing <see cref="RuleAttribute"/> methods that guard with
/// <see cref="Require"/> and mutate state.
/// </summary>
public abstract class ModelProgram
{
    /// <summary>
    /// A rule guard: if <paramref name="condition"/> is false the action is disabled in
    /// the current state (the engine skips it). Mirrors Spec Explorer's condition model.
    /// </summary>
    protected static void Require(bool condition, string reason)
    {
        if (!condition)
        {
            throw new GuardDisabledException(reason);
        }
    }

    /// <summary>
    /// An exploration bound (not a real precondition): if <paramref name="condition"/> is false the
    /// action is disabled here only to keep the explored state space finite. Unlike
    /// <see cref="Require"/>, a bound is NOT recorded as a model-derived negative edge — a conforming
    /// SUT is not expected to reject it, because the limit is an artifact of finite exploration
    /// rather than illegal behavior.
    /// </summary>
    protected static void RequireBound(bool condition, string reason)
    {
        if (!condition)
        {
            throw new GuardDisabledException(reason, isExplorationBound: true);
        }
    }
}
