namespace Sek.Core.Model;

/// <summary>
/// A directed edge in the exploration graph: taking <see cref="Action"/> in state
/// <see cref="FromStateId"/> leads to state <see cref="ToStateId"/>.
/// </summary>
public sealed record Transition(string FromStateId, ActionInvocation Action, string ToStateId);
