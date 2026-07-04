---
title: Combination strategies
description: Interaction (full product) versus pairwise coverage, and predicate pruning, in SpecExplorerKit parameter generation.
---

# Combination strategies

When an action has several parameters, the number of combinations grows with the
product of the domain sizes. **Combination strategies** control which combinations
SEK generates, letting you trade thoroughness for size. They are declared inside a
Cord `where {. ... .}` block.

## Interaction (full product) — the default

`Combination.Interaction(...)` — or simply not specifying a strategy — generates the
**full cartesian product** of the parameter domains. Every combination is explored.

```text
Condition.In(name, "@$^", "t.cmd", "t.exe");   // 3
Condition.In(time, -1, 60, 3600);              // 3
// frequency: enum, 3 members
// → 3 × 3 × 3 = 27 combinations
```

Use interaction when the space is small enough to explore exhaustively.

## Pairwise (2-wise)

`Combination.Pairwise(p1, p2, …)` generates a **minimal set of combinations such
that every pair of parameter values appears together at least once**. Pairwise
testing catches the large majority of interaction faults with dramatically fewer
cases.

```text
Condition.In(name, "@$^", "t.cmd", "t.exe");
Condition.In(time, -1, 60, 3600);
Combination.Pairwise(name, time, frequency);
// → 11 combinations instead of 27
```

SEK computes pairwise sets with a greedy set-cover that keeps the smallest subset
still covering every value-pair present in the full product.

## Predicate pruning

`Condition.IsTrue(expr)` removes combinations that violate a boolean predicate.
Combine it with either strategy:

```text
Condition.IsTrue(!(name == "t.cmd" & frequency != Frequency.Daily));
// removes the 6 offending combinations: 27 → 21
```

Predicates support comparisons (`== != < <= > >=`), logical (`&& || !`), arithmetic
(`+ - * / %`), and bitwise (`&` `|`) operators, and enum-qualified literals.

## Strategy at a glance

| Strategy | Combinations | Use when |
|---|---|---|
| `Interaction` (default) | full product | the space is small enough to explore fully |
| `Pairwise` | minimal 2-wise cover | many parameters; interaction is too large |
| `+ Condition.IsTrue` | pruned by predicate | some combinations are invalid or uninteresting |
| `Combination.Isolated(expr)` | one representative per predicate | test "special"/error values in isolation |
| `Combination.Seeded(e1, e2, …)` | guarantees a combination is present | pin an important case into the suite |
| `Combination.Expand(p…)` | forces full coverage of the listed params | override a pairwise reduction where needed |

## Refinements: Isolated, Seeded, Expand

Beyond `Interaction` and `Pairwise`, SEK supports Spec Explorer's refinements:

- **`Combination.Isolated(expr)`** keeps only one representative combination among
  those satisfying `expr`, so a rare/error value is tested without multiplying it
  across the whole matrix.
- **`Combination.Seeded(e1, e2, …)`** guarantees at least one combination satisfying
  the conjunction of predicates is included (a required seed case).
- **`Combination.Expand(p…)`** forces the listed parameters to be fully crossed even
  under a pairwise reduction.

## Evidence

The [ParameterGeneration sample](../samples/parameter-generation.md) demonstrates
each strategy on the same `AddJob(name, time, frequency)` action (full product = 27):

| Machine | Strategy | Combinations |
|---|---|---|
| `Product` / `Interaction` | full product | 27 |
| `Pairwise` | pairwise | 11 |
| `Constraint` | `Condition.IsTrue` prune | 21 |
| `Isolated` | two `Isolated` predicates | 13 |
| `Seeded` | pairwise + a seed | 11 |
| `Expand` | pairwise + `Expand(all)` | 27 |

## Related

- [Parameter generation (Z3)](parameter-generation.md)
- [The Cord language](cord-language.md)
