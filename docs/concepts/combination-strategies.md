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

## Evidence

The [ParameterGeneration sample](../samples/parameter-generation.md) demonstrates
all three: `Product`/`Interaction` → 27, `Pairwise` → 11, `Constraint` → 21.

## Roadmap

Spec Explorer additionally offered `Isolated`, `Seeded`, and `Expand` refinements.
SEK models `Interaction` and `Pairwise` today; the others are on the roadmap. When
not yet modeled, they degrade to `Interaction` (a superset), so results remain sound.

## Related

- [Parameter generation (Z3)](parameter-generation.md)
- [The Cord language](cord-language.md)
