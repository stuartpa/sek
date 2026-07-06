namespace SpecExplorerKit.Components.Solving;

/// <summary>The value kinds the solver understands.</summary>
public enum ValueKind
{
    Int,
    Long,
    Bool,
    String,
}

/// <summary>A parameter to generate values for, with an optional explicit candidate domain.</summary>
public sealed class SolverParam
{
    public string Name { get; init; } = string.Empty;
    public ValueKind Kind { get; init; } = ValueKind.Int;

    /// <summary>Explicit candidate values (from <c>Condition.In</c> or a model <c>[Domain]</c>).</summary>
    public IReadOnlyList<object?>? Domain { get; init; }
}

public abstract class SolverConstraint
{
}

/// <summary><c>Condition.In(param, v1, v2, ...)</c>: the parameter must be one of the listed values.</summary>
public sealed class InConstraint : SolverConstraint
{
    public string Param { get; init; } = string.Empty;
    public List<object?> Values { get; init; } = new();
}

/// <summary><c>Condition.IsTrue(expr)</c>: a boolean predicate over the parameters.</summary>
public sealed class PredicateConstraint : SolverConstraint
{
    public Expr Expr { get; init; } = null!;
}

/// <summary>
/// A boolean predicate over the parameters compiled from arbitrary embedded C# (via
/// Roslyn). Used when the expression is outside the mini-parser's grammar (e.g. uses
/// <c>Math.Abs</c>, string methods, <c>%</c>, or a ternary). Applied as a C# post-filter
/// by both solvers.
/// </summary>
public sealed class CompiledPredicateConstraint : SolverConstraint
{
    public string Source { get; init; } = string.Empty;
    public Func<IReadOnlyDictionary<string, object?>, bool> Predicate { get; init; } = _ => true;
}

// ---- Tiny expression tree for predicate constraints ---------------------------

public abstract class Expr
{
}

public sealed class VarExpr : Expr
{
    public string Name { get; init; } = string.Empty;
}

public sealed class LitExpr : Expr
{
    public object? Value { get; init; }
    public ValueKind Kind { get; init; }
}

public sealed class BinExpr : Expr
{
    public string Op { get; init; } = string.Empty; // == != < <= > >= + - * / % & | && ||
    public Expr Left { get; init; } = null!;
    public Expr Right { get; init; } = null!;
}

public sealed class UnExpr : Expr
{
    public string Op { get; init; } = string.Empty; // ! -
    public Expr Operand { get; init; } = null!;
}

/// <summary>How to combine per-parameter value choices into test combinations.</summary>
public sealed class CombinationSpec
{
    public enum Strategy
    {
        /// <summary>Full cartesian product of all satisfying values (Spec Explorer "Interaction").</summary>
        AllCombinations,

        /// <summary>Every pair of parameter values appears in at least one combination.</summary>
        Pairwise,
    }

    public Strategy Mode { get; set; } = CombinationSpec.Strategy.AllCombinations;

    /// <summary><c>Combination.Isolated(expr)</c>: keep only one representative combination
    /// among those satisfying <c>expr</c> (test "special"/error values in isolation).</summary>
    public List<Expr> Isolated { get; } = new();

    /// <summary><c>Combination.Seeded(e1, e2, ...)</c>: guarantee at least one combination
    /// satisfying the conjunction of the given predicates is included.</summary>
    public List<IReadOnlyList<Expr>> Seeded { get; } = new();

    /// <summary><c>Combination.Expand(p...)</c>: fully expand these parameters — every distinct
    /// value tuple they take must be represented, even under a pairwise reduction.</summary>
    public List<string> Expand { get; } = new();

    /// <summary>When set, pairwise reduction covers value-pairs of these named columns (each an
    /// expression over the parameters, e.g. <c>days &amp; 0x1</c>) instead of the raw parameters.
    /// Enables <c>Combination.Pairwise(name, time, days &amp; 0x1, ...)</c> over derived values.</summary>
    public List<(string Name, Expr Expr)> PairwiseColumns { get; } = new();
}
