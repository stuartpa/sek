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

    /// <summary>Free-form metadata (bounds hit, seed, timings, tool version, ...).</summary>
    public Dictionary<string, string> Metadata { get; } = new();

    public ModelState? FindState(string id) => States.FirstOrDefault(s => s.Id == id);

    public IEnumerable<Transition> OutgoingFrom(string stateId) =>
        Transitions.Where(t => t.FromStateId == stateId);
}
