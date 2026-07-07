# CRD002: SEK self-model exploration + conformance (SekSession CLI workflow)

- **Created:** 2026-07-06
- **Model:** MDL002 (`samples/SelfHost/`)
- **CORD:** `samples/SelfHost/Model/Config.cord`
- **Status:** COMPLETE (explored, generated, conformance green)

## Exploration

`construct model program from Actions` over `action all SekSession` →
**2 states / 5 transitions / 2 accepting** (bounds StateBound=16, StepBound=16, PathDepthBound=8).
The state space is the `Explored` toggle: from the initial state `Validate`/`Explore` are enabled;
once `Explored`, `View` is additionally enabled — matching the modelled guard.

## Conformance generation

`construct test cases for ModelProgram` (Long strategy) → **2 test paths covering 5/5 transitions**,
emitted as an xUnit project bound to the `SelfHost.Sut.SekSession` SUT.

## Conformance result

`dotnet test` on the generated project → **PASS (2/2)**. Each path instantiated `SekSession` and
replayed the modelled action sequence, which drove the **real `sek` CLI** (validate/explore/view)
against the Turnstile sample. The `View`-after-`Explore` ordering held against the actual tool.

## Coverage contribution

This is the CRD that satisfies the v1.4.0 gate's "explored + generates conformance" criterion for the
`Sek.Cli` vertical module. (Line/branch coverage of `Sek.Cli` is driven separately by the in-process
CLI integration tests — see COV004.)

## Related

- MDL002; COV004; regression manifest entry `samples/SelfHost / ModelProgram = 2/5/2`
- CI: "SEK self-validation loop" step
