namespace Sek.Solver;

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
}
