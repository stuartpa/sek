namespace Sek.Engine;

/// <summary>
/// Resolves a Cord configuration's <em>action universe</em> from its action declarations —
/// <c>action all &lt;AdapterType&gt;</c> (import every action of an adapter) and explicit
/// <c>action … Type.Method</c> declarations — against the model's rule labels.
///
/// <para>An <c>action all T</c> imports every rule whose label is qualified by <c>T</c> (label
/// segment before the last <c>.</c> equals <c>T</c>'s last segment). Explicit declarations
/// import the exactly-named rule. When no declaration resolves to any rule (e.g. the model uses
/// bare, unqualified labels), the whole rule set is imported — matching Spec Explorer's
/// "import all actions" intent for a single-model program.</para>
/// </summary>
public static class ActionImportResolver
{
    private static string LastSegment(string s)
    {
        var i = s.LastIndexOf('.');
        return i >= 0 ? s[(i + 1)..] : s;
    }

    private static string Qualifier(string label)
    {
        var i = label.LastIndexOf('.');
        return i >= 0 ? label[..i] : string.Empty;
    }

    /// <summary>
    /// The set of model rule labels permitted by a config's declarations. Returns all labels
    /// when nothing resolves (safe default for bare-labelled models).
    /// </summary>
    /// <param name="importedTypes">Types from <c>action all T</c>.</param>
    /// <param name="explicitActionLabels">Fully-qualified labels from explicit <c>action</c> declarations.</param>
    /// <param name="allRuleLabels">Every rule label the model exposes.</param>
    public static IReadOnlySet<string> Resolve(
        IEnumerable<string> importedTypes,
        IEnumerable<string> explicitActionLabels,
        IEnumerable<string> allRuleLabels)
    {
        var all = allRuleLabels.ToHashSet(StringComparer.Ordinal);
        var allowed = new HashSet<string>(StringComparer.Ordinal);

        var importedLeaves = importedTypes.Select(LastSegment).ToHashSet(StringComparer.Ordinal);
        foreach (var label in all)
        {
            if (importedLeaves.Contains(LastSegment(Qualifier(label))))
            {
                allowed.Add(label);
            }
        }

        foreach (var e in explicitActionLabels)
        {
            if (all.Contains(e)) allowed.Add(e);
            else if (all.Contains(LastSegment(e))) allowed.Add(LastSegment(e));
        }

        // Nothing resolved (e.g. bare labels that match no adapter type): import everything.
        return allowed.Count == 0 ? all : allowed;
    }
}
