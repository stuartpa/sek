# COV001: Turnstile — generated-test coverage

- **Created:** 2026-07-06
- **Stage:** 5 (coverage)
- **Model/Cord:** MDL001 / CRD001

## Result

The generated xUnit test (from `sek generate`) replays the explored path against the SUT and
passes, covering **3/3 explored transitions** — every turnstile transition
(`Locked→Coin→Unlocked`, `Unlocked→Coin→Unlocked`, `Unlocked→Push→Locked`). Both SUT methods
(`Turnstile.Coin`, `Turnstile.Push`) and both SUT states (`Locked`, `Unlocked`) are exercised, so
the SUT is fully covered by model-generated tests.

## The loop closed

- Model (MDL001) → explore (CRD001, 2/3/1) → generate → run against SUT → **green**.
- The one gap found on the way (IN001, stateful-SUT harness) was fixed, so the loop is repeatable
  and is now a CI gate.

## Note on the extracted component

The pilot's *extracted component* (`SpecExplorerKit.Components.Json.CanonicalJson`) is a pure
function and is covered by 5 direct unit tests in `tests/Sek.Tests/CanonicalJsonTests.cs`
(object-key sorting, array set-semantics, recursion, stable hashing, null) — see MDL001 for why a
pure component is unit-tested rather than SEK-modelled.

## Related

- MDL001, CRD001, IN001
