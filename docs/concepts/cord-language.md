---
title: The Cord language
description: A conceptual overview of Cord — SpecExplorerKit's configuration and scenario language.
---

# The Cord language

**Cord** (COndition and Reaction Descriptions) is SEK's language for *configuring*
exploration and *composing* behavior. This page explains the concepts; the
[Cord language reference](../reference/cord-language.md) has the full grammar.

## Two jobs

Cord does two things:

1. **Configuration** — `config` blocks declare which actions are in scope, the
   parameter domains and combination strategies, and switches (bounds/options).
2. **Behavior composition** — `machine` blocks name behaviors to explore, either by
   constructing the underlying model program or by composing abstract actions with
   the operator algebra.

## Configurations

```text
config Base
{
    action void Cart.AddItem(string sku, int qty);
    switch StateBound = 500;
}

config WithDomains : Base       // inheritance with ':'
{
    action void Cart.AddItem(string sku, int qty)
      where {.
        Condition.In(sku, "A", "B");
        Condition.In(qty, 1, 2, 3);
        Combination.Pairwise(sku, qty);
      .};
}
```

- `action ...` declares an action signature. `action all <Type>` imports a whole
  type's actions.
- The `where {. ... .}` block holds parameter constraints:
  [`Condition.In`, `Condition.IsTrue`, `Combination.*`](parameter-generation.md).
- `switch K = V;` sets a bound or option.
- `config Derived : Base` inherits actions, domains, and switches.

## Machines

```text
machine Explore() : WithDomains
{
    construct model program from WithDomains
}
```

- `construct model program from <Config>` — explore the model program under a config.
- `construct accepting paths for <machine>` — keep only paths ending in accepting states.
- `construct test cases [where ...] for <machine>` — derive test cases.

## Behavior algebra

Machines can also compose abstract actions directly:

```text
machine Party() : PartyActivities
{
    ( (Dance; Sing) | (Eat; Drink) ) ; KeepPartying?
}
```

| Operator | Meaning |
|---|---|
| `;` | tight sequence |
| `\|` | choice / union |
| `?` `*` `+` | optional / zero-or-more / one-or-more |
| `{n}` `{n,}` `{n,m}` | bounded repetition |
| `\|\|` `\|\|\|` `\|?\|` | synchronized / interleaved / sync-interleaved parallel |
| `->` | loose sequence |
| `&` | permutation |
| `_` | any single action |
| `...` | any sequence (`_*`) |
| `!` | negation of an atomic action |

The [Operators sample](../samples/operators.md) demonstrates every operator.

## Embedded C#

Cord can embed C# for constraints and preconstraints:

- `{. statements .}` — a C# statement block (e.g., in a `where` clause).
- `(. expression .)` — a C# expression.

## Relationship to the model

Action labels in Cord line up with `[Rule("Label")]` labels in the model.
`sek validate` reports any Cord action with no matching rule, or any `construct`
reference that doesn't resolve.

## Related

- [Cord language reference (grammar)](../reference/cord-language.md)
- [Parameter generation](parameter-generation.md)
- [Combination strategies](combination-strategies.md)
- Guide: [Writing Cord scenarios](../guides/writing-cord.md)
