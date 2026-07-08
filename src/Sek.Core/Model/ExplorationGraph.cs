namespace Sek.Core.Model;

/// <summary>
/// The transition-system intermediate representation (IR): the finite graph produced
/// by exploring a model program/machine. This is the single artifact that
/// visualization, test generation and conformance checking all consume.
/// </summary>
public sealed class ExplorationGraph
{
    /// <summary>Name of the machine that was explored.</summary>
    public string Machine { get; init; } = string.Empty;

    /// <summary>Id of the initial state (should also appear in <see cref="States"/>).</summary>
    public string? InitialStateId { get; set; }

    public List<ModelState> States { get; } = new();

    public List<Transition> Transitions { get; } = new();

    /// <summary>
    /// Model-derived <em>negative</em> transitions: for a reachable state, an action whose guard is
    /// unsatisfied there (so the model forbids it) together with the guard's reason. These are the
    /// substrate for negative conformance — a test/replay drives the legal prefix to
    /// <see cref="NegativeTransition.FromStateId"/> then attempts the action and asserts the SUT
    /// <em>rejects</em> it. They are not part of the reachable positive graph (the action changes no
    /// state); they record "attempting X here is illegal and must be refused".
    /// </summary>
    public List<NegativeTransition> NegativeTransitions { get; } = new();

    /// <summary>Free-form metadata (bounds hit, seed, timings, tool version, ...).</summary>
    public Dictionary<string, string> Metadata { get; } = new();

    public ModelState? FindState(string id) => States.FirstOrDefault(s => s.Id == id);

    public IEnumerable<Transition> OutgoingFrom(string stateId) =>
        Transitions.Where(t => t.FromStateId == stateId);
}

/// <summary>
/// A model-derived illegal (state, action) pair: attempting <see cref="Action"/> from
/// <see cref="FromStateId"/> is forbidden by the model (the action's guard is false there), and a
/// conforming SUT must reject it. <see cref="Reason"/> is the guard's message.
/// </summary>
public sealed record NegativeTransition(string FromStateId, ActionInvocation Action, string Reason);
