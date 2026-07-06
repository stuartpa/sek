using Sek.Cord.Ast;

namespace Sek.Cord.Semantics;

/// <summary>
/// The checked, resolved result of the semantic-analysis phase (ARC001, phase 3). It bundles the
/// <see cref="SymbolTable"/> (resolved names) with the <see cref="DiagnosticBag"/> (problems found)
/// and is the artifact the back end (IR build / exploration / code generation) consumes — never the
/// raw text or the CLI.
/// </summary>
public sealed class SemanticModel
{
    public SemanticModel(CordDocument document, SymbolTable symbols, DiagnosticBag diagnostics)
    {
        Document = document;
        Symbols = symbols;
        Diagnostics = diagnostics;
    }

    public CordDocument Document { get; }

    public SymbolTable Symbols { get; }

    public DiagnosticBag Diagnostics { get; }

    public bool HasErrors => Diagnostics.HasErrors;

    /// <summary>Adapter types imported via <c>action all T</c> visible to a machine.</summary>
    public IReadOnlyList<string> ImportedActionTypes(string machine) => Symbols.ImportedActionTypes(machine);

    /// <summary>Effective declared actions visible to a machine (across its base configs).</summary>
    public IReadOnlyDictionary<string, DeclaredAction> DeclaredActions(string machine) => Symbols.DeclaredActions(machine);

    /// <summary>Short labels of a machine's <c>event</c> actions (uncontrollable observations).</summary>
    public IReadOnlySet<string> EventActions(string machine) => Symbols.EventActions(machine);
}
