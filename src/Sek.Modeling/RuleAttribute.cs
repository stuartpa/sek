namespace Sek.Modeling;

/// <summary>
/// Marks a method as a model rule (an action). During exploration the engine invokes
/// the rule with values drawn from each parameter's <see cref="DomainAttribute"/>; if
/// the rule's guards (see <see cref="ModelProgram.Require"/>) pass, the resulting state
/// change becomes a transition labeled with the action.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RuleAttribute : Attribute
{
    /// <summary>
    /// Optional action label (e.g. <c>Warehouse.CreateWarehouse</c>). When omitted the
    /// engine derives the label from the declaring type and method name.
    /// </summary>
    public string? Action { get; }

    public RuleAttribute()
    {
    }

    public RuleAttribute(string action)
    {
        Action = action;
    }
}
