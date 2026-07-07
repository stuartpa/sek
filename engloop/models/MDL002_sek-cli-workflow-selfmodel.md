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

`SekWorkflowModel : ModelProgram` — state: `bool Explored`.
- `SekSession.Validate` — always available.
- `SekSession.Explore` — sets `Explored`.
- `SekSession.View` — **guarded** on `Explored` (a graph must exist to render).
- `[AcceptingCondition] Done()` — any state may end.

This captures the tool's **real ordering constraint**: `sek view` can only render a graph that a
prior `sek explore` produced.

## The SUT (`samples/SelfHost/Sut/SekSession.cs`)

`SelfHost.Sut.SekSession` drives the **real** `sek` CLI (subprocess) against the Turnstile sample:
`Validate()` → `sek validate`, `Explore()` → `sek explore ModelProgram`, `View()` → `sek view`
(throws if no prior explore, mirroring the modelled guard). The SUT is the actual tool, so the
generated tests validate SEK's genuine behavior — not a re-implementation (avoids the PM002 theatre
trap).

## Result

- `sek explore ModelProgram --project samples/SelfHost` → **2 states, 5 transitions, 2 accepting**
  (recorded in the regression manifest; CI-protected).
- `sek generate ModelProgram --project samples/SelfHost` → 2 xUnit conformance tests covering 5/5
  transitions.
- `dotnet test` on the generated project → **PASS (2/2)**: the generated tests drove the real `sek`
  CLI through the modelled workflow and conformed.
- CI runs the full loop ("SEK self-validation loop" step in `.github/workflows/ci.yml`).

## Significance

The recursion is proven end-to-end for the `Sek.Cli` module: **SEK (built as an EngLoopKit consumer)
uses SEK to model, explore, and generate conformance tests for SEK's own CLI**, and they pass.

## Related

- CRD002 (the exploration + conformance run); COV004 (coverage state)
- Gate: EngLoopKit v1.4.0 vertical criterion (MDL + CRD + generated conformance)
