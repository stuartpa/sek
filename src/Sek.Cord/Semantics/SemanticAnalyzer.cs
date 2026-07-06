using Sek.Cord.Ast;

namespace Sek.Cord.Semantics;

/// <summary>
/// Phase 3 of the Cord compiler (ARC001): semantic analysis. Consumes the parsed
/// <see cref="CordDocument"/> (the syntax phase's output), builds the <see cref="SymbolTable"/>,
/// resolves names, and collects <see cref="Diagnostic"/>s for cross-reference problems that the
/// lexer and parser cannot see (duplicate declarations, references to undeclared configs/machines).
/// Produces a <see cref="SemanticModel"/> that the IR-build / exploration back end consumes.
/// </summary>
/// <remarks>
/// This is the pure-Cord layer of semantic analysis: it needs no reflected model type. Reflection-
/// dependent checks (does a declared action map to a model rule? is the action universe non-empty?)
/// remain with the caller that has loaded the model, but they are phrased as the same
/// <see cref="Diagnostic"/> vocabulary and merged into the run's bag.
/// </remarks>
public static class SemanticAnalyzer
{
    /// <summary>
    /// Analyzes <paramref name="document"/>. When <paramref name="targetMachine"/> is supplied, also
    /// verifies that machine exists (the machine an <c>explore</c>/<c>test</c> run targets).
    /// </summary>
    public static SemanticModel Analyze(CordDocument document, string? targetMachine = null)
    {
        var diagnostics = new DiagnosticBag();
        var symbols = new SymbolTable(document);

        CheckDuplicateNames(document, diagnostics);
        CheckBaseConfigReferences(document, symbols, diagnostics);
        CheckConstructReferences(document, symbols, diagnostics);

        if (targetMachine is not null && !symbols.IsMachine(targetMachine))
        {
            diagnostics.Error(
                "SEM006",
                $"machine '{targetMachine}' not found (available: {string.Join(", ", document.Script.Machines.Select(m => m.Name))})");
        }

        return new SemanticModel(document, symbols, diagnostics);
    }

    // SEM001 / SEM002 — a name declared twice is ambiguous (later merge would silently shadow).
    private static void CheckDuplicateNames(CordDocument document, DiagnosticBag diagnostics)
    {
        foreach (var dup in document.Script.Configurations.GroupBy(c => c.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            diagnostics.Error("SEM001", $"configuration '{dup.Key}' is declared {dup.Count()} times");
        }

        foreach (var dup in document.Script.Machines.GroupBy(m => m.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            diagnostics.Error("SEM002", $"machine '{dup.Key}' is declared {dup.Count()} times");
        }
    }

    // SEM003 / SEM004 — a base config that isn't declared. Warning (not error): an external/implicit
    // base should not fail a run, but it is almost always a typo worth surfacing.
    private static void CheckBaseConfigReferences(CordDocument document, SymbolTable symbols, DiagnosticBag diagnostics)
    {
        foreach (var cfg in document.Script.Configurations)
        {
            foreach (var b in cfg.BaseConfigs.Where(b => !symbols.IsConfiguration(b)))
            {
                diagnostics.Warning("SEM003", $"configuration '{cfg.Name}' inherits from unknown configuration '{b}'");
            }
        }

        foreach (var machine in document.Script.Machines)
        {
            foreach (var b in machine.BaseConfigs.Where(b => !symbols.IsConfiguration(b)))
            {
                diagnostics.Warning("SEM004", $"machine '{machine.Name}' inherits from unknown configuration '{b}'");
            }
        }
    }

    // SEM005 — a `construct … for <ref>` whose reference is neither a config nor a machine.
    private static void CheckConstructReferences(CordDocument document, SymbolTable symbols, DiagnosticBag diagnostics)
    {
        foreach (var machine in document.Script.Machines)
        {
            var construct = machine.Body?.FindConstruct();
            if (construct is null) continue;
            var refName = construct.Reference;
            if (string.IsNullOrEmpty(refName)) continue; // uses a nested `for` behavior instead of a name
            if (!symbols.IsConstructReference(refName))
            {
                diagnostics.Error("SEM005", $"machine '{machine.Name}' constructs from unknown '{refName}'");
            }
        }
    }
}
