---
title: Writing Cord scenarios
description: Use the Cord language to declare configurations, parameter domains, bounds, and behavior machines.
---

# Writing Cord scenarios

**Cord** is SEK's scenario and configuration language. It lives in `.cord` files
and does two jobs: it *configures* exploration (which actions, what parameter
domains, what bounds) and it *composes behavior* into machines. This guide is
practical; the [Cord language reference](../reference/cord-language.md) has the
full grammar.

## Configurations

A `config` declares the actions in scope, parameter domains, and switches (bounds
and options). Configs can inherit with `:`.

```text
using MyApp.Model;

config Base
{
    action void Cart.AddItem(string sku, int qty);
    action void Cart.Checkout();

    switch StateBound = 500;
    switch StepBound  = 5000;
    switch TestEnabled = false;
}

config WithDomains : Base
{
    action void Cart.AddItem(string sku, int qty)
      where {.
        Condition.In(sku, "A", "B");
        Condition.In(qty, 1, 2, 3);
        Combination.Pairwise(sku, qty);
      .};
}
```

Inside a `where {. ... .}` block you can use:

- `Condition.In(param, v1, v2, ...)` — the parameter's candidate values.
- `Condition.IsTrue(expr)` — a boolean predicate that prunes combinations (Z3).
- `Combination.Pairwise(...)` — 2-wise coverage instead of the full product.
- `Combination.Interaction(...)` — full product (the default).

See [Parameter generation](../concepts/parameter-generation.md) and
[Combination strategies](../concepts/combination-strategies.md).

## Switches (bounds and options)

| Switch | Meaning |
|---|---|
| `StateBound` | Maximum number of states before exploration stops. |
| `StepBound` | Maximum number of transitions. |
| `PathDepthBound` | Maximum path depth from the initial state. |
| `TestEnabled` | Marks test-suite machines (informational for `sek`). |

## Machines

A `machine` names a behavior to explore. The most common form asks SEK to explore
the underlying model program:

```text
machine Explore() : WithDomains
{
    construct model program from WithDomains
}
```

You can also compose **behavior** directly over abstract actions using the Cord
operator algebra (this is *behavior mode* — no model program required):

```text
machine Party() : PartyActivities
{
    ( (Dance; Sing) | (Eat; Drink) ) ; KeepPartying?
}
```

### Behavior operators

| Operator | Meaning |
|---|---|
| `;` | tight sequence |
| `\|` | choice (union) |
| `?` `*` `+` | optional / zero-or-more / one-or-more |
| `{n}` `{n,}` `{n,m}` | bounded repetition |
| `\|\|` `\|\|\|` `\|?\|` | synchronized / interleaved / sync-interleaved parallel |
| `->` | loose sequence |
| `&` | permutation |
| `_` | any single action |
| `...` | any sequence (`_*`) |
| `!` | negation of an atomic action |

The [Operators sample](../samples/operators.md) demonstrates every one.

## Explore it

```bash
sek explore Explore --project path/to/project
sek explore Party   --project path/to/project   # behavior mode
```

## Tips

- Put shared actions and bounds in a base `config`; add domains in a derived one.
- If exploration hits a bound, tighten the scenario or reduce domains rather than
  raising bounds — an unbounded graph usually means the scenario is under-specified.
- Keep action signatures in Cord aligned with your model rule labels; `sek validate`
  flags mismatches.
