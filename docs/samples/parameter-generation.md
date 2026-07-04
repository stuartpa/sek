---
title: ParameterGeneration sample
description: Z3-powered parameter generation with interaction, pairwise, and predicate-pruning combination strategies.
---

# ParameterGeneration

**Demonstrates:** Z3-backed parameter generation and combination strategies.

- **Project:** `samples/ParameterGeneration`
- **Model:** `PG.SUT` — a stateless `AddJob(string name, int time, Frequency frequency)`

## What it covers

The same action explored under different Cord combination strategies. Because the
model is stateless, the number of transitions equals the number of generated
parameter combinations:

| Machine | Strategy | Transitions |
|---|---|---|
| `Product` | interaction (full product) | 27 |
| `Interaction` | explicit interaction | 27 |
| `Pairwise` | `Combination.Pairwise` | 11 |
| `Constraint` | `Condition.IsTrue` predicate pruning | 21 |
| `Isolated` | `Combination.Isolated` (×2) | 13 |
| `Seeded` | pairwise + `Combination.Seeded` | 11 |
| `Expand` | pairwise + `Combination.Expand` | 27 |

Each machine uses the classic sample's form `AddJob || construct model program from <Cfg>`
— combining a one-action scenario (slicing) with the model, so both parameter generation
and scenario slicing are exercised together.

- `name` ∈ 3 values, `time` ∈ 3 values, `frequency` ∈ `{Once, Daily, Weekly}` →
  27 for the full product.
- Pairwise reduces 27 → 11 while covering every value-pair.
- The predicate `!(name == "t.cmd" & frequency != Frequency.Daily)` prunes 6
  combinations → 21, with the enum literal `Frequency.Daily` resolved by the engine.
- `Isolated` tests special values in isolation (27 → 13); `Expand` forces the full
  cross-product despite a pairwise reduction (→ 27).

## Run it

```bash
dotnet build samples/ParameterGeneration/Model/ParameterGeneration.Model.csproj
sek explore Product     --project samples/ParameterGeneration
sek explore Pairwise    --project samples/ParameterGeneration
sek explore Constraint  --project samples/ParameterGeneration
```

## Why it matters

This is the proof that [Z3 parameter generation](../concepts/parameter-generation.md)
and [combination strategies](../concepts/combination-strategies.md) work end to end:
interaction, pairwise reduction, and predicate pruning with enum literals.

## Related

- [Parameter generation (Z3)](../concepts/parameter-generation.md)
- [Combination strategies](../concepts/combination-strategies.md)
