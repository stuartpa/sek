using SpecExplorerKit.Components.Solving;

namespace Sek.Engine;

/// <summary>Solver-backed parameter generation configuration for the explorer.</summary>
public sealed class ParameterGeneration
{
    /// <summary>The solver that generates value combinations (Z3 by default).</summary>
    public IParameterSolver Solver { get; init; } = new EnumerativeSolver();

    /// <summary>Per-action constraints (keyed by action label), sourced from Cord <c>where</c> clauses.</summary>
    public IReadOnlyDictionary<string, ActionParamSpec> ByAction { get; init; } =
        new Dictionary<string, ActionParamSpec>();

    /// <summary>Maximum value combinations to generate per action per state.</summary>
    public int Limit { get; init; } = 100000;
}

/// <summary>Constraints + combination strategy for one action's parameters.</summary>
public sealed class ActionParamSpec
{
    public IReadOnlyList<SolverConstraint> Constraints { get; init; } = new List<SolverConstraint>();
    public CombinationSpec Combination { get; init; } = new();
}
