namespace Sek.Core.Model;

/// <summary>
/// A node in the exploration graph: one reachable state of the model program.
/// </summary>
/// <param name="Id">Stable, human-facing id within a graph (e.g. <c>S0</c>).</param>
/// <param name="Hash">
/// Canonical content hash of the model state, used to detect revisited states during
/// exploration (deterministic de-duplication).
/// </param>
/// <param name="Label">Optional short description of the state.</param>
/// <param name="Accepting">Whether the state satisfies the model's accepting conditions.</param>
/// <param name="Initial">Whether this is the exploration's initial state.</param>
public sealed record ModelState(
    string Id,
    string Hash,
    string? Label = null,
    bool Accepting = false,
    bool Initial = false);
