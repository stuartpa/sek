namespace SpecExplorerKit.Components.Random;

/// <summary>
/// A reproducible, seeded Bernoulli probability gate. Seeded from an integer, so a given seed
/// always yields the same sequence of decisions (reproducible sampling), while different seeds
/// produce different sequences. Successive calls draw sequentially from the same stream.
/// </summary>
/// <remarks>
/// Generic and domain-free (EngLoopKit component pattern): it wraps only <see cref="System.Random"/>
/// and carries no SpecExplorerKit concept. SpecExplorerKit's Cord front-end composes it to power
/// <c>Probability.IsTrue(p)</c> parameter-generation branches, but the gate itself knows nothing
/// about Cord.
/// </remarks>
public sealed class ProbabilityGate
{
    private readonly System.Random _rng;

    public ProbabilityGate(int seed) => _rng = new System.Random(seed);

    /// <summary>Draws once: true with probability <paramref name="p"/> (clamped to [0,1]).
    /// <c>p &gt;= 1</c> is always true; <c>p &lt;= 0</c> is always false.</summary>
    public bool IsTrue(double p)
    {
        if (p >= 1.0) return true;
        if (p <= 0.0) return false;
        return _rng.NextDouble() < p;
    }
}
