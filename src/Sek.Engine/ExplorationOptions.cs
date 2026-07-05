namespace Sek.Engine;

/// <summary>Bounds and knobs controlling a bounded exploration.</summary>
public sealed class ExplorationOptions
{
    /// <summary>Maximum number of distinct states to generate.</summary>
    public int MaxStates { get; set; } = 12800;

    /// <summary>Maximum number of transitions (steps) to generate.</summary>
    public int MaxTransitions { get; set; } = 12800;

    /// <summary>Maximum BFS depth from the initial state.</summary>
    public int MaxDepth { get; set; } = 12800;

    /// <summary>When true (Cord <c>switch StopAtError</c>), a model-checking exploration stops
    /// as soon as the first failure (<c>: fail</c>) state is reached.</summary>
    public bool StopAtError { get; set; }
}
