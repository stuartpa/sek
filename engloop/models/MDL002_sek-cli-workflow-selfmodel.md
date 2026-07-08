# MDL002: SEK CLI-workflow self-model — SEK validates SEK

- **Created:** 2026-07-06
- **Module modelled:** `sek` / `Sek.Cli` (the CLI vertical)
- **Sample:** `samples/SelfHost/`
- **Status:** EXPLORED + GENERATED + CONFORMANCE-GREEN

## What this is

The first genuine **self-model**: SEK modelling SEK. It answers the maintainer's requirement that
"SEK itself must be modelled by SEK and explored and test generated." The `sek` CLI vertical is
domain behavior (a stateful command workflow), so it is verified by a SEK model + exploration +
generated conformance tests — not by hand-written unit tests (per the v1.4.0 gate, PM002).

## The model (`samples/SelfHost/Model/SelfHostModel.cs`)

`SekWorkflowModel : ModelProgram` — state: **`bool Initialized; bool Explored`** (rebuilt 2026-07-08
for EngLoopKit **PM004**: a self-model must be behaviorally rich **and** prove model-derived negative
conformance). The guards encode the CLI's *real* ordering rules against a **fresh** project:
- `SekSession.Init` — always legal; sets `Initialized` (writes the project config).
- `SekSession.Validate` / `Explore` / `Generate` / `Test` — **guarded on `Initialized`** (the CLI has
  no config to load before `init`). `Explore` sets `Explored`.
- `SekSession.View` — **guarded on `Explored`** (only an `explore` produces a graph to render).
- `[AcceptingCondition] Done() => Explored`.

The two interacting bits give **genuine branching** (init → {validate/explore/generate/test} → view),
and — crucially — the guards let SEK **derive the illegal (state, action) pairs** (e.g. `explore`
before `init`, `view` before `explore`). No error case is hand-coded (the old `ViewMissing`/
`ExploreUnknown` shortcut is gone): the model's guards do the work, and `sek generate`/`sek test` turn
the illegal pairs into **negative conformance** tests that assert the real CLI rejects them.

## The SUT (`samples/SelfHost/Sut/SekSession.cs`)

`SelfHost.Sut.SekSession` drives the **real** `sek` CLI (subprocess). Each instance owns a **fresh,
un-initialised** workspace (the Turnstile Cord, pointed at the real Turnstile model/adapter
assemblies), so the ordering rules are exercised *for real*: before `Init()` there is no config, so
`validate`/`explore`/`generate`/`test` genuinely error; `view` errors until an `explore` wrote a
graph. Every action either succeeds or **throws** — so the model-derived negative tests (attempt an
illegal action, assert rejection) verify the real CLI's real error behaviour, not a hand-coded assert.

## Result

- `sek explore ModelProgram --project samples/SelfHost` → **3 states, 12 transitions, 1 accepting**
  (plus **6 model-derived negative edges**; recorded in the regression manifest; CI-protected).
- `sek test ModelProgram --project samples/SelfHost` → conformance **14/14 positive replayed,
  6 negative replayed / 6 rejected (SUT correctly refused), TEST PASSED**.
- `sek generate ModelProgram --project samples/SelfHost` → **2 positive** tests covering 12/12
  transitions **plus 6 negative** (illegal-action rejection) tests.
- `dotnet test` on the generated project → **PASS (8/8)**: positive paths drove the real CLI through
  the branching lifecycle; the 6 negative tests asserted the real CLI **rejects** each illegal action.
- CI runs the full loop ("SEK self-validation loop" step in `.github/workflows/ci.yml`).
- **PM004 adequacy:** Negative-conformance? **Y** (6 model-derived rejection tests). Branches? **Y**
  (2 interacting state bits → 3 reachable states, distinct interleavings).

## Significance

The recursion is proven end-to-end for the `Sek.Cli` module: **SEK (built as an EngLoopKit consumer)
uses SEK to model, explore, and generate conformance tests for SEK's own CLI**, and they pass.

## Related

- CRD002 (the exploration + conformance run); COV004 (coverage state)
- Gate: EngLoopKit v1.4.0 vertical criterion (MDL + CRD + generated conformance)
