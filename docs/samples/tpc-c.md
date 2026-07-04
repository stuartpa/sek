---
title: TPC-C sample
description: A large model with full conformance replay — the flagship end-to-end proof of SpecExplorerKit.
---

# TPC-C

**Demonstrates:** a large, realistic model **and** full conformance replay against a
system under test — the flagship end-to-end proof of SEK.

- **Project:** the `SpecExplorer/` TPC-C model (the original proof project)
- **Model:** `Model.TpccModel` with an `Adapter` binding to a fake implementation

## What it covers

The TPC-C benchmark schema and its five transactions (New-Order, Payment,
Order-Status, Delivery, Stock-Level) plus the schema-creation actions, modeled with
`[Domain]`-driven parameters and accepting conditions for consistency invariants.

- **Explore:** 2,446 states / 12,706 transitions / 1,054 accepting.
- **Conformance (`sek test`):** all **12,706** transitions replayed against the
  fake implementation, **0 failed**, all **10** actions covered → **TEST PASSED**.

## Run it

```bash
# from the SpecExplorer project
sek explore TpccExploration --project path/to/SpecExplorer
sek test    TpccExploration --project path/to/SpecExplorer
```

## Why it matters

TPC-C exercises the whole loop at scale: a non-trivial state space, Z3/enumerative
parameter generation, deterministic exploration, and a complete conformance replay
against a real binding. It is the strongest single demonstration that SEK works end
to end.

## Related

- [Conformance](../concepts/conformance.md)
- Guide: [Running conformance](../guides/conformance.md)
