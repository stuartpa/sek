namespace Sek.Solver;

/// <summary>
/// A reproducible probability gate for Cord's <c>Probability.IsTrue(p)</c> parameter-generation
/// branches. Seeded from the Cord <c>switch RandomSeed</c>, so a given seed always yields the
/// same sequence of decisions (matching Spec Explorer's <em>reproducible seeded sampling</em>
/// intent), while different seeds explore different branches. Successive calls within one
/// <c>where</c> block draw sequentially from the same stream.
/// </summary>
public sealed class ProbabilityGate
{
    private readonly Random _rng;

    public ProbabilityGate(int seed) => _rng = new Random(seed);

    /// <summary>Draws once: true with probability <paramref name="p"/> (clamped to [0,1]).
    /// <c>p &gt;= 1</c> is always true; <c>p &lt;= 0</c> is always false.</summary>
    public bool IsTrue(double p)
    {
        if (p >= 1.0) return true;
        if (p <= 0.0) return false;
        return _rng.NextDouble() < p;
    }
}
