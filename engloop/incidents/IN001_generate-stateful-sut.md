# IN001: `sek generate` could not drive a stateful SUT (fresh instance per step)

- **Created:** 2026-07-06
- **Stage:** 6 (incident) — surfaced during the EngLoopKit pilot (Stage 5 generate loop)
- **Severity:** correctness (generated tests unusable for any stateful SUT)
- **Status:** RESOLVED (fixed, committed, tested)

## Symptom

The first binding sample (Turnstile) generated an xUnit test that **failed**:
`System.InvalidOperationException : cannot push a locked turnstile` at `Turnstile.Push()`.

## Root cause

The generated test harness (`Sek.Cli/TestGen.cs`) created a **fresh SUT instance on every step**
(`Activator.CreateInstance(type)` inside `Step`). A stateful SUT therefore reset between steps, so
the replayed path `Coin; Coin; Push` ran `Push` on a brand-new, locked turnstile.

## Timeline

| # | Action | Type |
|---|---|---|
| MIT001 | Reproduced with the Turnstile sample; confirmed per-step `Activator.CreateInstance` | diagnosis |
| — | Harness caches one instance per SUT type, reused across a path's steps (xUnit still news a fresh test class + Sut per `[Fact]`, so paths stay isolated) | **fix** |

## Fix (a repair, not just a mitigation — per the Golden Rule)

`TestGen.cs` harness now holds `Dictionary<Type,object> _instances` and resolves the SUT target via
`Instance(type)` (lazy get-or-create). Committed `7bc6739`, built, and verified: the Turnstile
generated test passes; a CI step (`generate` → `dotnet test`) guards it permanently.

## Learning

`sek generate`'s replay semantics require **one SUT instance per test path**. This is now the
documented + enforced behavior. (Static-method SUTs — e.g. the atsvc/Account style — were
unaffected; the bug only bit instance/stateful SUTs, which no in-repo sample had until Turnstile.)

## Related

- CRD001 / MDL001 / COV001 (pilot); the `using-sek-to-generate-tests` skill (update candidate).
