# REF001: Extract generic components (Random, Graphs) per ARC002

- **Date:** 2026-07-06
- **Cadence:** architect-driven convergence cycle (implementing ARC002)
- **Budget:** n/a (directed work under the ARC001/ARC002 mandate)
- **Status:** CHOSEN (implemented directly — see commit `f736fe4`)
- **Emitted SEED:** none (implemented in-cycle rather than handed to a fresh Delivery loop)

## Signals gathered

| Signal | Finding |
|---|---|
| Recurring cause-classes (POSTMORTEM INDEX) | none yet (no post-mortems) |
| Architecture drift / boundary violations | ARC002 boundary not yet realized: several generic, domain-free modules still live inside vertical assemblies |
| Duplicated business logic (DRY) | reachability BFS was inlined twice inside `GraphAnalysis` (backward fixpoint + local Backward/Forward) |
| Hot spots (change frequency × complexity) | `Sek.Solver`/`Sek.Core.Analysis` touched across recent steering/probability work |
| Test speed vs coverage | fine (suite ~2s) |

## Decision-tree branch taken

**Branch 3 — non-vertical code still living in the vertical (component leakage).** ARC002's litmus
test (*useful, unchanged, in an unrelated repo?*) flags `ProbabilityGate` (a seeded Bernoulli gate)
and the pure directed-graph reachability algorithms as generic. Branch 1 (recurring cause-class) and
branch 2 (guard-reported drift) had no concrete signal yet; component leakage was the highest-value
fire and the ARC002 doc had already named these candidates.

## Chosen refactor

Two tightly-scoped extractions (the component pattern's "one component per cycle", done as a pair
because both were pure lifts named in ARC002):

1. `ProbabilityGate` → `components/SpecExplorerKit.Components.Random` (a seeded reproducible
   Bernoulli gate; `Sek.Cord` composes it).
2. The pure reachability algorithms → `components/SpecExplorerKit.Components.Graphs`
   (`Reachability.Backward`/`Forward` over abstract `(from, to)` edges + comparer).
   `Sek.Core.Analysis.GraphAnalysis` becomes the **domain adapter** that projects `ExplorationGraph`
   transitions onto edges and delegates — this is the "generic graph seam" ARC002 asked for, and it
   also removes the duplicated inline BFS.

## Expected long-term benefit

Generic building blocks become independently-testable, liftable assemblies; the vertical's intent is
clearer; the one-way `vertical → components` dependency rule is now demonstrated across three
components (Json, Random, Graphs), so future generic code has an obvious home.

## Rationale for not choosing the others

- Branch 1/2: no recurring cause-class or guard-reported drift with a concrete fix yet.
- Branch 4 (DRY): the reachability duplication was folded into this extraction rather than a separate
  cycle.
- Branch 5/6: coverage adequate; suite well within budget.

## Hand-off

Implemented directly (ARC002 mandate). Guarded by 5 new `ReachabilityTests` + the existing
probability tests + the 60-sample regression gate (all green). Remaining ARC002 candidates
(`Combinatorics` core, renderers, Z3/Roslyn glue) recorded in ARC002 for later cycles.
