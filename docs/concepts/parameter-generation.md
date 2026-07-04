---
title: Parameter generation (Z3)
description: How SpecExplorerKit uses the Z3 theorem prover to generate action parameters from declarative Cord constraints.
---

# Parameter generation (Z3)

Actions often take parameters. **Parameter generation** is the process of turning
declarative constraints into concrete argument combinations to explore. SEK uses the
**Z3 theorem prover** (via `Microsoft.Z3`) as its solver, with a dependency-free
enumerative solver as a cross-check and fallback.

## Where domains come from

For each rule parameter, SEK determines a candidate **domain**:

| Source | When it applies |
|---|---|
| **Cord `Condition.In(p, v1, v2, …)`** | value parameters (`int`, `string`, `enum`, `bool`) |
| **`[Domain("Method")]`** | a method returns candidates; supports *state-dependent* domains |
| **Natural domain** | `enum` → all members; `bool` → `{false, true}` |
| **Reachable objects** | reference-typed parameters — see [Object domains](object-domains.md) |

## What Z3 does

Given the domains and constraints for an action, SEK builds an SMT problem:

- Each parameter becomes an SMT constant (integers for numeric/enum values, booleans,
  and an index for finite string domains).
- `Condition.In` becomes a membership constraint.
- `Condition.IsTrue(expr)` becomes a boolean/arithmetic predicate (`== != < <= > >=`,
  `&& || !`, `+ - * / %`, and bitwise `&` `|`). Enum-qualified literals such as
  `Frequency.Daily` are resolved to their underlying values.
- Z3 then **enumerates all satisfying assignments**, blocking each model it finds so
  the next is different. Predicates Z3 can't represent are applied as a C# post-filter.

The result is exactly the set of parameter combinations that satisfy your
constraints — no more, no less.

## A worked example

From the [ParameterGeneration sample](../samples/parameter-generation.md):

```text
action static void SUT.AddJob(string name, int time, Frequency frequency)
  where {.
    Condition.In(name, "@$^", "t.cmd", "t.exe");
    Condition.In(time, -1, 60, 3600);
    Condition.IsTrue(!(name == "t.cmd" & frequency != Frequency.Daily));
  .};
```

- `name` ∈ 3 values, `time` ∈ 3 values, `frequency` ∈ `{Once, Daily, Weekly}`
  (natural enum domain) → 27 combinations for the full product.
- The `Condition.IsTrue` predicate prunes the 6 combinations where
  `name == "t.cmd"` and `frequency != Daily`, leaving **21**.

Exploring the `Product` machine yields 27 transitions; `Constraint` yields 21 —
demonstrating Z3-driven pruning.

## Choosing the solver

```bash
sek explore <machine> --solver z3     # default
sek explore <machine> --solver enum   # dependency-free enumerative solver
```

On small models the two agree exactly; running both is a good sanity check. Z3
scales to larger domains and richer predicates.

## Performance

Value-parameter domains and constraints are state-independent, so SEK **caches** the
solver result per action, turning what would be one solve per state into one solve
per action. Object- and floating-point-typed parameters are enumerated directly
(their domains can be state-dependent) rather than sent to Z3.

## Self-test

```bash
sek z3      # prints SATISFIABLE and a sample model — confirms the native Z3 library loaded
```

## Related

- [Combination strategies](combination-strategies.md)
- [Object domains](object-domains.md)
- [The Cord language](cord-language.md)
