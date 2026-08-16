---
title: Operators sample
description: The implemented Cord operator set, demonstrated over abstract actions in behavior mode.
---

# Operators

**Demonstrates:** every implemented Cord behavior operator in its supported root form, explored in
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
| `InterleavedParallel` | `\|\|\|` | 27 / 50 / 9 |
| `SyncInterleavedParallel` | `\|?\|` | 16 / 23 / 7 |
| `TightSequence` | `;` | 10 / 15 / 3 |
| `LooseSequence` | `->` | 11 / 68 / 3 |
| `Permutation` | `&` | 9 / 8 / 2 |
| `ZeroOrMore` / `OneOrMore` / `Optional` | `*` `+` `?` | 6/12/4 · 6/12/3 · 10/17/4 |
| `BoundedRepetitionExact` / `…Least` / `…Range` | `{n}` `{n,}` `{n,m}` | 9/14/3 · 13/29/6 · 13/23/6 |
| `AnyAction` / `RepetitionOfAnyAction` | `_` / `...` | 4/13/1 · 3/13/2 |
| `Negation` / `Truncation` | `!` / `construct accepting paths` | 3/11/2 · 1/0/1 |

All 18 machines explore successfully.

## Run it

```bash
sek explore Party --project samples/Operators
sek explore InterleavedParallel --project samples/Operators
pwsh samples/run-operators.ps1     # runs them all
```

## Why it matters

The Operators sample is the proof that SEK's [Cord behavior algebra](../reference/cord-language.md#behavior-operators)
can be explored without any model state in these supported forms — the
[behavior-mode](../concepts/state-exploration.md#behavior-mode) exploration path.
See the [support matrix](../reference/cord-support.md) before composing parallel operators
beneath other algebra.

## Related

- [The Cord language](../concepts/cord-language.md)
- [Cord language reference](../reference/cord-language.md)
- [Cord operator semantics](../reference/cord-operators.md)
