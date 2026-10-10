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

    public long MaxCandidateEvaluations { get; set; } = 250000;
    public int MaxNegativeTransitions { get; set; } = 5000;
    public TimeSpan MaxElapsedTime { get; set; } = TimeSpan.FromSeconds(120);
    public CancellationToken CancellationToken { get; set; }
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    public ExplorationBudget? Budget { get; set; }
    public Func<string, string, Sek.Core.Model.ActionInvocation, bool>? RejectionFilter { get; set; }

    /// <summary>When true (Cord <c>switch StopAtError</c>), a model-checking exploration stops
    /// as soon as the first failure (<c>: fail</c>) state is reached.</summary>
    public bool StopAtError { get; set; }

    /// <summary>Optional goal-state predicate for steering constructs (Cord <c>point shoot …
    /// with (. expr .)</c>). Evaluated against each explored model instance; matching state ids
    /// are recorded in <c>graph.Metadata["goals"]</c> so the caller can prune to reaching paths.</summary>
    public Func<object, bool>? GoalPredicate { get; set; }

    /// <summary>Optional restriction of the model's action universe to the rule labels imported by
    /// a Cord config (<c>action all</c> / explicit <c>action</c> declarations). Null means every
    /// rule is enabled.</summary>
    public IReadOnlySet<string>? AllowedActionLabels { get; set; }

    /// <summary>Short labels of actions declared <c>event</c> (Cord <c>action event …</c>). A
    /// transition on one of these is tagged as an observation (<c>ActionInvocation.Kind =
    /// "event"</c>) rather than a controllable call.</summary>
    public IReadOnlySet<string>? EventActionLabels { get; set; }
}
