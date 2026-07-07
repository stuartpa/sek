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

`SekWorkflowModel : ModelProgram` — state: `bool Explored`. Enriched (2026-07-07) to the **full CLI
command surface** plus the tool's **error behaviour** as first-class transitions:
- `SekSession.Init` / `Validate` / `Explore` / `Generate` / `Test` — always available (each is
  independently runnable against a pre-initialised project). `Explore` sets `Explored`.
- `SekSession.View` — **guarded** on `Explored` (a graph must exist to render).
- `SekSession.ViewMissing` — **error transition**: `sek view <missing>` must fail (non-zero exit).
- `SekSession.ExploreUnknown` — **error transition**: `sek explore <unknown-machine>` must fail.
- `[AcceptingCondition] Done() => Explored` — the accepting scenario is "the core exploration loop
  has run" (a graph exists).

This captures the tool's **one real ordering constraint** (`sek view` can only render a graph a
prior `sek explore` produced) while exercising every command. Adding further ordering would be
fiction against a pre-initialised project (PM003: behaviour-level, non-theatre).

## The SUT (`samples/SelfHost/Sut/SekSession.cs`)

`SelfHost.Sut.SekSession` drives the **real** `sek` CLI (subprocess) against the Turnstile sample:
`Init/Validate/Explore/View/Generate/Test` invoke the corresponding `sek` commands and assert exit 0;
`ViewMissing`/`ExploreUnknown` invoke `sek` and assert a **non-zero** exit (the tool's error paths).
`View` throws if no prior explore, mirroring the modelled guard. The SUT is the actual tool, so the
generated tests validate SEK's genuine behavior — not a re-implementation (avoids the PM002 theatre
trap).

## Result

- `sek explore ModelProgram --project samples/SelfHost` → **2 states, 15 transitions, 1 accepting**
  (recorded in the regression manifest; CI-protected).
- `sek test ModelProgram --project samples/SelfHost` → conformance **16/16 transitions replayed,
  8 actions covered, TEST PASSED**.
- `sek generate ModelProgram --project samples/SelfHost` (Long strategy) → 2 xUnit conformance tests
  covering **15/15** transitions.
- `dotnet test` on the generated project (in-repo) → **PASS (2/2)**: the generated tests drove the
  real `sek` CLI through the full command surface — including both error paths — and conformed.
- CI runs the full loop ("SEK self-validation loop" step in `.github/workflows/ci.yml`).

## Significance

The recursion is proven end-to-end for the `Sek.Cli` module: **SEK (built as an EngLoopKit consumer)
uses SEK to model, explore, and generate conformance tests for SEK's own CLI**, and they pass.

## Related

- CRD002 (the exploration + conformance run); COV004 (coverage state)
- Gate: EngLoopKit v1.4.0 vertical criterion (MDL + CRD + generated conformance)
