# MDL001: Turnstile — SEK model of the pilot binding SUT

- **Created:** 2026-07-06
- **Stage:** 4 (model)
- **SUT:** `samples/Turnstile/Sut` — `Turnstile.Sut.Turnstile` (stateful: `Locked`)
- **Model:** `samples/Turnstile/Model/Model.cs` — `Turnstile.Model.TurnstileModel`

## Purpose

The EngLoopKit pilot (HANDOFF §4) needs one small surface taken all the way through
architect → model → explore → coverage, chosen to shake out the `sek` model→generate→SUT loop
cheaply. The skill `using-sek-to-generate-tests` noted that **no in-repo SEK sample had ever wired
a `binding`**, so `sek generate` against a real SUT was entirely unexercised. The turnstile is the
canonical minimal *stateful* model-based-testing subject — the smallest surface that genuinely
exercises state exploration + replay.

## The model

State: one boolean `Locked` (starts `true`). Rules mirror the SUT methods (label
`Turnstile.<Method>` binds to SUT class `Turnstile`):

| Rule | Guard | Effect |
|---|---|---|
| `Turnstile.Coin` | — | `Locked = false` |
| `Turnstile.Push` | `Require(!Locked)` | `Locked = true` |

Accepting condition: `AtRest() => Locked` (a test may end with the turnstile at rest).

## Modeling learning (applicability boundary)

SEK models **stateful** SUTs as transition systems. Pure functions (e.g. the extracted component
`SpecExplorerKit.Components.Json.CanonicalJson`, a `string → string` map) have no state to explore
and are verified by direct property-style unit tests instead — not by a SEK model. The pilot
therefore models the turnstile (stateful) rather than the extracted pure component; the component
is covered by its own 5 unit tests. This is the honest boundary of the model stage.

## Related

- ARC001 (compiler phases), ARC002 (component boundary)
- CRD001 (exploration + generate loop), COV001 (coverage), IN001 (generate harness fix)
