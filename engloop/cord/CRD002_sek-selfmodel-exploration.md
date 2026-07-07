# CRD002: SEK self-model exploration + conformance (SekSession CLI workflow)

- **Created:** 2026-07-06
- **Model:** MDL002 (`samples/SelfHost/`)
- **CORD:** `samples/SelfHost/Model/Config.cord`
- **Status:** COMPLETE (explored, generated, conformance green)

## Exploration

`construct model program from Actions` over `action all SekSession` →
**2 states / 15 transitions / 1 accepting** (bounds StateBound=16, StepBound=32, PathDepthBound=8).
The state space is the `Explored` toggle. From the initial state the always-runnable commands
(`Init`/`Validate`/`Explore`/`Generate`/`Test`) and both error transitions
(`ViewMissing`/`ExploreUnknown`) are enabled; once `Explored`, `View` is additionally enabled —
matching the modelled guard. The single accepting state is the explored state.

## Conformance generation

`construct test cases for ModelProgram` (Long strategy) → **2 test paths covering 15/15 transitions**,
emitted as an xUnit project bound to the `SelfHost.Sut.SekSession` SUT. `sek test` replays the same
graph directly for **16/16** transitions across **8** actions (TEST PASSED).

## Conformance result

`dotnet test` on the generated project (in-repo) → **PASS (2/2)**. Each path instantiated `SekSession`
and replayed the modelled action sequence, which drove the **real `sek` CLI** — the full command
surface (init/validate/explore/view/generate/test) plus both error paths — against the Turnstile
sample. The `View`-after-`Explore` ordering held against the actual tool.

## Coverage contribution

This is the CRD that satisfies the v1.4.0 gate's "explored + generates conformance" criterion for the
`Sek.Cli` vertical module. (Line/branch coverage of `Sek.Cli` is driven separately by the in-process
CLI integration tests — see COV004.)

## Related

- MDL002; COV004; regression manifest entry `samples/SelfHost / ModelProgram = 2/15/1`
- CI: "SEK self-validation loop" step
