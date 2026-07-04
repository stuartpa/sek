---
title: Sailboat sample
description: A stateful navigation model with pairwise parameters and state-dependent domains.
---

# Sailboat

**Demonstrates:** a stateful model with real computation in rules, pairwise
parameter generation, and **state-dependent `[Domain]`** methods.

- **Project:** `samples/Sailboat`
- **Model:** `Sailboat.Model.SailboatModel`

## What it covers

`Sail(Heading, hours, knots)` moves the boat using trigonometry and clamps to the
shore; `RunAground(atx, aty)` and `Rescue()` model the grounding lifecycle. The
`Sail` domains (`hours`, `knots`) and pairwise strategy come from Cord;
`RunAground`'s `atx`/`aty` use **state-dependent `[Domain]`** methods
(`=> new[]{X}` / `=> new[]{Y}`) so the guard `atx == X` is always satisfiable.

**Result:** 4,000 states / 4,000 transitions / 3 accepting **(bound hit)** — matching
the classic sample's own 4,000 bound.

## Run it

```bash
dotnet build samples/Sailboat/Model/Sailboat.Model.csproj
sek explore Voyage --project samples/Sailboat
```

## Why it matters

Sailboat shows that rules can run arbitrary C# (here, `Math.Cos`/`Math.Sin`), that
parameters can be generated pairwise, and that a `[Domain]` method can depend on the
*current state* — a general capability, not sample-specific.

## Related

- [Parameter generation](../concepts/parameter-generation.md) (state-dependent `[Domain]`)
- [Combination strategies](../concepts/combination-strategies.md)
