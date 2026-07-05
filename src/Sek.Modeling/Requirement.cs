namespace Sek.Modeling;

/// <summary>
/// Requirement-capture API used inside model rules (Cord's <c>Requirement.Capture(id)</c>).
/// A model calls <see cref="Capture(string)"/> when the current step satisfies a numbered
/// specification requirement; the exploration engine brackets each rule invocation with
/// <see cref="Reset"/> / <see cref="Captured"/> to attribute the captured requirement ids to
/// that transition and to compute overall requirement coverage.
/// </summary>
public static class Requirement
{
    [ThreadStatic] private static List<string>? _captured;

    /// <summary>Records that the current step covers requirement <paramref name="id"/>.</summary>
    public static void Capture(string id)
    {
        _captured ??= new List<string>();
        if (!string.IsNullOrWhiteSpace(id)) _captured.Add(id);
    }

    /// <summary>Engine hook: clears the capture buffer before a rule invocation.</summary>
    public static void Reset() => _captured = null;

    /// <summary>Engine hook: the requirement ids captured since the last <see cref="Reset"/>.</summary>
    public static IReadOnlyList<string> Captured => _captured ?? (IReadOnlyList<string>)Array.Empty<string>();
}
