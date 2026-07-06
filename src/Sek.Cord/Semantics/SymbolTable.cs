using Sek.Cord.Ast;

namespace Sek.Cord.Semantics;

/// <summary>The category of a Cord <see cref="Symbol"/>.</summary>
public enum SymbolKind
{
    Configuration,
    Machine,
    DeclaredAction,
    ImportedActionType,
}

/// <summary>A named entity discovered during semantic analysis.</summary>
/// <param name="Name">The Cord name (config/machine name, action target, adapter type).</param>
/// <param name="Kind">What kind of entity it is.</param>
public sealed record Symbol(string Name, SymbolKind Kind);

/// <summary>
/// The single source of truth for Cord names (ARC001). Every later phase and every CLI command
/// resolves configurations, machines, declared actions, imported adapter types and per-machine
/// effective facts through this table rather than re-deriving them from the raw
/// <see cref="CordDocument"/>. It is built once by the <see cref="SemanticAnalyzer"/>.
/// </summary>
/// <remarks>
/// The declaration-level registry (which names exist, and duplicate detection) is owned here; the
/// inheritance-aware effective resolutions (switches, declared actions, imported types, event
/// actions) still delegate to <see cref="CordDocument"/>'s walkers for now, but this table is the
/// one place callers ask — so the walkers can be migrated in behind this seam without touching
/// call sites (a Stage-3 convergence step recorded in ARC001).
/// </remarks>
public sealed class SymbolTable
{
    private readonly CordDocument _document;
    private readonly Dictionary<string, Symbol> _configs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Symbol> _machines = new(StringComparer.Ordinal);

    public SymbolTable(CordDocument document)
    {
        _document = document;
        foreach (var c in document.Script.Configurations)
        {
            _configs.TryAdd(c.Name, new Symbol(c.Name, SymbolKind.Configuration));
        }

        foreach (var m in document.Script.Machines)
        {
            _machines.TryAdd(m.Name, new Symbol(m.Name, SymbolKind.Machine));
        }
    }

    public IReadOnlyCollection<Symbol> Configurations => _configs.Values;

    public IReadOnlyCollection<Symbol> Machines => _machines.Values;

    public bool IsConfiguration(string name) => _configs.ContainsKey(name);

    public bool IsMachine(string name) => _machines.ContainsKey(name);

    /// <summary>True when <paramref name="name"/> is a known config or machine — the resolution a
    /// <c>construct … for &lt;ref&gt;</c> reference must satisfy.</summary>
    public bool IsConstructReference(string name) => IsConfiguration(name) || IsMachine(name);

    // --- effective, inheritance-aware facts (single access point) ---------------------------

    public Machine? GetMachine(string name) => _document.GetMachine(name);

    public Configuration? GetConfiguration(string name) => _document.GetConfiguration(name);

    public IReadOnlyList<string> ImportedActionTypes(string machine) =>
        _document.ResolveMachineImportedActionTypes(machine);

    public IReadOnlyDictionary<string, DeclaredAction> DeclaredActions(string machine) =>
        _document.ResolveMachineDeclaredActions(machine);

    public IReadOnlySet<string> EventActions(string machine) =>
        _document.ResolveMachineEventActions(machine);
}
