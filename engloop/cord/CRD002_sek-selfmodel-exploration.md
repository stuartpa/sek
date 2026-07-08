# CRD002: SEK self-model exploration + conformance (SekSession CLI workflow)

- **Created:** 2026-07-06
- **Model:** MDL002 (`samples/SelfHost/`)
- **CORD:** `samples/SelfHost/Model/Config.cord`
- **Status:** COMPLETE (explored, generated, conformance green)

## Exploration

`construct model program from Actions` over `action all SekSession` →
**3 states / 12 transitions / 1 accepting**, plus **6 model-derived negative edges** (bounds
StateBound=16, StepBound=32, PathDepthBound=8). The state space is the pair `(Initialized, Explored)`.
From the fresh state only `Init` is enabled (the other commands are guard-disabled → negative edges);
once `Initialized`, `Validate`/`Explore`/`Generate`/`Test` are enabled; once `Explored`, `View` is
too — matching the modelled guards. The accepting state is the explored state.
matching the modelled guard. The single accepting state is the explored state.

## Conformance generation

`construct test cases for ModelProgram` (Long strategy) → **2 positive test paths covering 12/12
transitions PLUS 6 negative (illegal-action rejection) tests**, emitted as an xUnit project bound to
the `SelfHost.Sut.SekSession` SUT. `sek test` replays the same graph directly: **14/14 positive +
6 negative replayed / 6 rejected** (TEST PASSED).

## Conformance result

`dotnet test` on the generated project → **PASS (8/8)**. The 2 positive paths instantiated
`SekSession` and drove the **real `sek` CLI** through the branching lifecycle; the 6 **model-derived
negative** tests drove the legal prefix to each forbidding state then asserted the real CLI **rejects**
the illegal action (`explore`/`validate`/`generate`/`test` before `init`; `view` before `explore`).
No error case is hand-coded — the negatives fall out of the model's guards (EngLoopKit PM004).

## Coverage contribution

This is the CRD that satisfies the v1.4.0 gate's "explored + generates conformance" criterion for the
`Sek.Cli` vertical module. (Line/branch coverage of `Sek.Cli` is driven separately by the in-process
CLI integration tests — see COV004.)

## Related

- MDL002; COV004; regression manifest entry `samples/SelfHost / ModelProgram = 3/12/1`
- CI: "SEK self-validation loop" step
