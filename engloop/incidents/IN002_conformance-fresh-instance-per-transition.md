# IN002: `sek test` conformance replayed a fresh SUT instance per transition (stateful-SUT bug)

- **Started:** 2026-07-06 (surfaced by the SEK CLI-workflow self-model, MDL002)
- **Reported by:** SEK self-model conformance run (`sek test ModelProgram --project samples/SelfHost`)
- **Affected:** `Sek.Cli.Conformance.Replay` (the `sek test` command)
- **Status:** RESOLVED
- **Cause-class:** bug-regression (stateful-SUT handling), same class as IN001 but in the `test` path

## Symptom

Running `sek test` against the self-model SUT reported a spurious conformance failure:

```
SekSession.View: view requires a graph produced by a prior explore
```

The model guards `View` on a prior `Explore`; the exploration only ever reaches `View` after
`Explore`. Yet conformance reported `View` failing — and the same defect made the **Turnstile**
`sek test` fail (`Push` "cannot push a locked turnstile").

## Root cause

`Conformance.Replay` iterated the graph's transitions and, for **each transition**, invoked the SUT
method on a **freshly `Activator.CreateInstance`d** object:

```csharp
method.Invoke(method.IsStatic ? null : Activator.CreateInstance(type!), args);
```

So SUT state never carried between steps. A guarded action whose precondition is established by an
earlier action in the path (`View` needs a prior `Explore`; `Push` needs a prior `Coin`) was replayed
on a fresh instance in its initial state → the guard threw → spurious "conformance failure." This is
the **same class as IN001** (the generated harness new-ing a fresh SUT per call), but in the `sek test`
conformance path rather than `sek generate`.

## Fix (permanent — landed in source, not a mitigation)

Rewrote `Conformance.Replay` to replay **witness paths** (init → accepting, via `TestGen.SelectPaths`)
rather than isolated transitions, using **one SUT instance per path** (get-or-create per type), and
to stop a path on first failure (its state is then indeterminate). This mirrors the generated
harness's per-path instance reuse (the IN001 fix).

## Verification

- `sek test ModelProgram --project samples/SelfHost` → **PASSED** (5/5 transitions, 3 actions).
- `sek test ModelProgram --project samples/Turnstile` → **PASSED** (3/3) — the same fix resolved the
  Turnstile mismatch too (Coin precedes Push, so Push is never replayed on a locked turnstile).
- Full unit + integration suite green; 61-sample exploration regression unaffected (uses `explore`).

## Significance

This is the intended payoff of "SEK validates SEK": the CLI-workflow self-model (MDL002) surfaced a
**real correctness bug** in SEK's own conformance command, which was then fixed. The `test` and
`generate` paths now share the same correct per-path, one-instance replay semantics.

## Related

- Same class as IN001 (generate harness). MDL002/CRD002 (the self-model that found it).
