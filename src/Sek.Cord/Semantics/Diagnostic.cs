namespace Sek.Cord.Semantics;

/// <summary>Severity of a <see cref="Diagnostic"/> raised by a compiler phase.</summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// A single positioned message produced by a compiler phase (lexical, syntax, or semantic
/// analysis). Diagnostics are <em>collected</em> rather than thrown so a run can report every
/// problem at once (per ARC001: each phase owns its diagnostics).
/// </summary>
/// <param name="Severity">Whether this stops compilation (<see cref="DiagnosticSeverity.Error"/>)
/// or is advisory.</param>
/// <param name="Code">Stable machine-readable code (e.g. <c>SEM001</c>).</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Line">1-based source line, or 0 when unknown.</param>
/// <param name="Column">1-based source column, or 0 when unknown.</param>
public sealed record Diagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    int Line = 0,
    int Column = 0)
{
    public override string ToString() =>
        Line > 0
            ? $"{Code} ({Line},{Column}): {Severity.ToString().ToLowerInvariant()}: {Message}"
            : $"{Code}: {Severity.ToString().ToLowerInvariant()}: {Message}";
}

/// <summary>
/// An accumulating collection of <see cref="Diagnostic"/>s for one compilation run. A phase adds
/// to the bag; the driver inspects <see cref="HasErrors"/> and reports <see cref="Items"/>.
/// </summary>
public sealed class DiagnosticBag
{
    private readonly List<Diagnostic> _items = new();

    public IReadOnlyList<Diagnostic> Items => _items;

    public bool HasErrors => _items.Any(d => d.Severity == DiagnosticSeverity.Error);

    public int ErrorCount => _items.Count(d => d.Severity == DiagnosticSeverity.Error);

    public void Add(Diagnostic diagnostic) => _items.Add(diagnostic);

    public void Error(string code, string message, int line = 0, int column = 0) =>
        _items.Add(new Diagnostic(DiagnosticSeverity.Error, code, message, line, column));

    public void Warning(string code, string message, int line = 0, int column = 0) =>
        _items.Add(new Diagnostic(DiagnosticSeverity.Warning, code, message, line, column));

    public void Info(string code, string message, int line = 0, int column = 0) =>
        _items.Add(new Diagnostic(DiagnosticSeverity.Info, code, message, line, column));
}
