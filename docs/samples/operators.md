---
title: Operators sample
description: The full Cord behavior algebra, demonstrated over abstract actions in behavior mode.
---

# Operators

**Demonstrates:** the complete Cord behavior operator algebra, explored in
*behavior mode* (no model program — pure Cord over abstract actions).

- **Project:** `samples/Operators`
- **Mode:** behavior (no model assembly; `Config.cord` only)

## What it covers

A family of machines over abstract "party" and "regular" activities, each isolating
one operator:

| Machine | Operator | Example result (states/transitions/accepting) |
|---|---|---|
| `Party` | `;` `\|` `?` | 6 / 6 / 3 |
| `SyncParallel` | `\|\|` | 3 / 2 / 1 |
| `InterleavedParallel` | `\|\|\|` | 30 / 58 / 9 |
| `SyncInterleavedParallel` | `\|?\|` | — |
| `TightSequence` | `;` | — |
| `LooseSequence` | `->` | — |
| `Permutation` | `&` | 9 / 8 / 2 |
| `ZeroOrMore` / `OneOrMore` / `Optional` | `*` `+` `?` | — |
| `BoundedRepetitionExact` / `…Least` / `…Range` | `{n}` `{n,}` `{n,m}` | — |
| `AnyAction` / `RepetitionOfAnyAction` | `_` / `...` | — |
| `Negation` / `Truncation` | `!` / `construct accepting paths` | — |

All 18 machines explore successfully.

## Run it

```bash
sek explore Party --project samples/Operators
sek explore InterleavedParallel --project samples/Operators
pwsh samples/run-operators.ps1     # runs them all
```

## Why it matters

The Operators sample is the proof that SEK's [Cord behavior algebra](../reference/cord-language.md#behavior-operators)
is implemented correctly and can be explored without any model state — the
[behavior-mode](../concepts/state-exploration.md#behavior-mode) exploration path.

## Related

- [The Cord language](../concepts/cord-language.md)
- [Cord language reference](../reference/cord-language.md)
