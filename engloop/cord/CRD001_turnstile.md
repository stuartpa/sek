# CRD001: Turnstile — exploration and generated-test loop

- **Created:** 2026-07-06
- **Stage:** 5 (explore)
- **Cord:** `samples/Turnstile/Model/Config.cord`
- **Model:** MDL001

## Exploration

`machine ModelProgram() : Actions { construct model program from Actions }` over the turnstile
model explores to:

```
Explored 'ModelProgram': 2 states, 3 transitions, 1 accepting.
```

- S0 `Locked` (initial, accepting) — `Coin` → S1 `Unlocked`
- S1 `Unlocked` — `Coin` → S1 (idempotent), `Push` → S0
- Recorded as a CI regression baseline (`samples/regression.manifest.json`).

## Generate loop (the point of the pilot)

`sek generate ModelProgram --project samples/Turnstile` emits a standalone xUnit project that
replays the explored path (`Coin; Coin; Push`) against the real SUT via reflection on the
`binding`. This is the first time `sek generate` was exercised against a real binding in-repo.

- **Finding → IN001:** the generated harness created a *fresh* SUT instance per step, so `Push`
  ran on a freshly-locked turnstile and threw — the test failed. Fixed (one instance per test
  path); the generated test now passes.
- Wired into CI as a permanent gate (build SUT+model → `sek generate` → `dotnet test` the
  generated project).

## Related

- MDL001, COV001, IN001; ARC001/ARC002
