# COV003: Re-baseline under the v1.4.0 class-based Readiness Gate — still NOT READY

- **Created:** 2026-07-06
- **Phase:** line-driving
- **Status:** OPEN
- **Readiness Gate:** **FAIL — NOT READY FOR INCIDENTS** (but the three components now PASS)

Supersedes COV002's framing. EngLoopKit v1.4.0 (PM002) keys the verification *method* to the module
*class*: **components** are verified by unit/property tests (≥95%, no MDL/CRD); the **domain
vertical** is verified by SEK self-modelling (`MDL`+`CRD` generating conformance tests, ≥95%); and
generic code left in the vertical is an ARC002 violation → FAIL. This re-baseline classifies SEK's
modules accordingly.

## Readiness Inventory (v1.4.0 gate)

| Module | Class | MDL? | CRD? | Line% | Branch% | Conformant? | PASS/FAIL |
|---|---|---|---|---|---|---|---|
| components/SpecExplorerKit.Components.Json | component | n/a | n/a | 100% | 100% | yes | **PASS** |
| components/SpecExplorerKit.Components.Random | component | n/a | n/a | 100% | 100% | yes | **PASS** |
| components/SpecExplorerKit.Components.Graphs | component | n/a | n/a | 100% | 100% | yes | **PASS** |
| Sek.Core | vertical | no | no | 97.5% | 83.8% | ARC001 (partial) | **FAIL** (branch <95%; no MDL/CRD; holds generic renderers) |
| Sek.Modeling | vertical | no | no | 88.4% | 73.4% | yes | **FAIL** |
| Sek.Cord | vertical | no | no | 71.8% | 57.2% | ARC001 (phase in place) | **FAIL** |
| Sek.Solver | vertical→**should be component(s)** | n/a | n/a | 50.3% | 36.8% | ARC002 candidate | **FAIL** (generic solver still in the vertical) |
| Sek.Engine | vertical | partial | no | 45.8% | 34.7% | yes | **FAIL** |
| sek / Sek.Cli | vertical | no | no | 10.2% | 7.5% | ARC001 (partial) | **FAIL** (Program.cs not testable/modelable) |

**Net:** 3 / 9 modules PASS (the extracted components). The vertical is the remaining work.

## The path to PASS (the maintainer's model: SEK validates SEK once components are factored out)

**Phase A — finish factoring generics out of the vertical (ARC002), unit/property-test each to ≥95%:**
- `Sek.Solver` is a generic *"constraint spec → satisfying assignments"* engine (Z3 glue, Roslyn
  predicate host, enumerative solver, combinatorics, predicate eval, expr tree). It carries no SEK
  domain concept → extract to `components/SpecExplorerKit.Components.Solving` (already ~50% unit-
  covered here; finish to ≥95%, incl. `Z3Solver`).
- `Sek.Core` renderers (DOT/Mermaid/HTML) are generic *graph* rendering behind a small seam →
  candidate `components/SpecExplorerKit.Components.GraphRendering`. (The IR types stay vertical.)
- Re-check every vertical file against the ARC002 litmus test; move anything domain-free out.

**Phase B — the residual vertical is domain behavior → model it with SEK (dogfood):**
- `Sek.Cord` (the Cord compiler pipeline: lex→parse→semantic→IR), `Sek.Engine` (exploration as a
  state machine), and the `sek` CLI **command lifecycle** (no-project → loaded → explored →
  generated → tested) are genuinely stateful domain behavior. Author an `MDL` + `CRD` for each and
  `sek generate` its conformance tests. This is the literal "SEK validates SEK."

**Phase C — unblock the CLI (prerequisite for B on `sek`):** extract the command bodies out of
top-level `Program.cs` into a testable/modelable `Sek.Cli` library type (a REF; ARC001 also wants
this). Until then `sek` can be neither unit-tested in-process nor modelled.

## Readiness Gate verdict

- [ ] PASS
- [x] **FAIL — NOT READY.** PASS rows: the 3 components. Failing rows: all vertical modules. Next:
  Phase A extraction of `Sek.Solver` → `Components.Solving`, then Phase C CLI refactor, then Phase B
  self-models.

> Per PM001/PM002, SEK will not be called "ready for incidents" until this verdict is PASS.

## Related

- Gate: EngLoopKit v1.4.0 (PM002/IN002), `docs/standards.md` Readiness Gate
- Supersedes framing of: COV002
