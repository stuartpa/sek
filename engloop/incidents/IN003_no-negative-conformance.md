# Incident IN003: SEK does only positive conformance — no model-derived negative testing

- **Started:** 2026-07-08 (surfaced by the maintainer reviewing SEK's self-generated tests; codified
  by EngLoopKit **PM004**, which now requires model-derived negative conformance in the readiness gate)
- **Affected:** `Sek.Engine.Explorer` (exploration), `Sek.Cli.TestGen` (generation),
  `Sek.Cli.Conformance` (replay) — the whole model → explore → generate/conform loop
- **Status:** RESOLVED (negative-conformance capability shipped; SelfHost rebuilt as a rich model)
- **Cause-class:** capability-gap (the tool can only prove *legal sequences succeed*, never that
  *illegal sequences are correctly rejected*)

## Symptom

A SEK model expresses **guards** (`Require(cond, reason)`) that make an action legal only in certain
states. But SEK's loop is **positive-only**:

- **Explorer** invokes each rule and, on a `GuardDisabledException`, treats the action as simply
  *disabled* — the transition is **dropped** (`Explorer.cs`: `continue; // guard disabled … not
  enabled`). So **illegal (state, action) pairs are never recorded**, hence never generated.
- **`Conformance.Replay`** invokes each action along a witness path and counts *any exception* as a
  failure — there is **no** "attempt this action illegally, assert the SUT rejects it with error X".
- **`TestGen`** emits only positive witness paths.

So SEK cannot generate the negative tests the strengthened readiness gate (EngLoopKit PM004) now
requires, and self-models resort to **hand-coding** error cases as always-enabled positive actions
(the SEK `SelfHost` `ViewMissing`/`ExploreUnknown` shortcut) — which PM004 classifies as theatre.

## Fix (capability)

Teach the loop to derive and check negative behavior **from the model's guards**:

1. **Explorer** — when a rule is guard-disabled in a reachable state, record a **negative transition**
   `(fromState, actionLabel, guardReason)` (distinct from a real invocation error). Carry them on the
   `ExplorationGraph`.
2. **Seexpl** — persist negative transitions so `view`/regeneration see them.
3. **TestGen** — for each reachable negative transition, emit a **negative test**: replay the shortest
   legal prefix to `fromState`, then attempt the illegal action via a harness `StepExpectingError`
   that **asserts the SUT rejects it** (throws / errors), optionally matching the modelled reason.
4. **Conformance** — replay negative transitions too: attempt the illegal action, assert rejection;
   an illegal action the SUT *accepts* is a conformance **failure**.

This makes negative conformance **model-derived** (guards + rejection), not hand-coded — satisfying
PM004. Then `samples/SelfHost` can be rebuilt as a **rich** model (real ordering state) whose illegal
orderings (`view` before `explore`, use before `init`) generate asserted-error tests.

## Verification (definition of done)

- [x] `sek explore` records negative transitions for guard-disabled actions. (SelfHost: 6.)
- [x] `sek generate` emits negative tests that assert the SUT rejects illegal actions. (6 emitted.)
- [x] `sek test` replays negatives and fails if an illegal action is accepted. (`NegativeConformanceTests`
      proves a non-conforming SUT that accepts an illegal action makes conformance FAIL.)
- [x] `samples/SelfHost` is a rich model (state `Initialized`,`Explored` → 3 states, distinct
      interleavings) whose generated suite shows real branching **and** model-derived negative tests;
      `dotnet test` → 8/8 green; `sek test` → 14 positive + 6/6 negative rejected.
- [x] MDL002/CRD002 updated; regression manifest `SelfHost = 3/12/1`; readiness gate's
      `Neg-conf?`/`Branches?` are `Y` for the CLI vertical.
