# COV002: Whole-product Readiness Gate baseline — NOT READY

- **Created:** 2026-07-06
- **Phase:** line-driving
- **Status:** OPEN
- **Readiness Gate:** **FAIL — NOT READY FOR INCIDENTS**

This is the first whole-product coverage measurement under EngLoopKit v1.3.0's Readiness Gate
(the fix from EngLoopKit PM001). It exists to make SEK's real status objective: SEK is **not**
ready for incidents. It was previously (and wrongly) called ready; it is not.

## Coverage (whole product, measured with coverlet)

| Metric | Baseline (start) | Current | Target |
|---|---|---|---|
| Line | 32.2% | **49.2%** | 95%+ |
| Branch | 24.0% | **37.5%** | 95%+ |
| Suite runtime | — | ~4 s (141 unit tests) | (fast) |

## Readiness Inventory

> One row per module. The gate PASSES only when **every** row is modelled + explored + covered
> ≥95% line & branch + conformant + green. It does not.

| Module | MDL? | CRD? | Line% | Branch% | Conformant? | PASS/FAIL |
|---|---|---|---|---|---|---|
| components/SpecExplorerKit.Components.Graphs | ⚠ unit-only | ⚠ unit-only | 100% | 100% | yes (ARC002) | **FAIL** (no MDL/CRD) |
| components/SpecExplorerKit.Components.Json | ⚠ unit-only | ⚠ unit-only | 100% | 100% | yes (ARC002) | **FAIL** (no MDL/CRD) |
| components/SpecExplorerKit.Components.Random | ⚠ unit-only | ⚠ unit-only | 100% | 100% | yes (ARC002) | **FAIL** (no MDL/CRD) |
| Sek.Core (vertical) | no | no | 97.5% | 83.8% | ARC001 (partial) | **FAIL** (branch <95%, no MDL/CRD) |
| Sek.Modeling (vertical) | no | no | 88.4% | 73.4% | yes | **FAIL** |
| Sek.Cord (vertical) | no | no | 71.8% | 57.2% | ARC001 (phase in place) | **FAIL** |
| Sek.Solver (vertical) | no | no | 50.3% | 36.8% | yes | **FAIL** (Z3Solver largely uncovered) |
| Sek.Engine (vertical) | partial | no | 45.8% | 34.7% | yes | **FAIL** |
| sek / Sek.Cli (vertical) | no | no | 10.2% | 7.5% | ARC001 (partial) | **FAIL** (see blocker below) |

## Progress this cycle (readiness-gate coverage drive)

Added targeted unit suites, raising overall line coverage 32.2% → 49.2%:
- `Sek.Solver` 1.4% → 50.3% (PredicateEval, EnumerativeSolver, Combinatorics, RoslynPredicate).
- `Sek.Cord` 43% → 71.8% (parser/lexer/AST/CordDocument + all in-repo `.cord` scripts parse).
- `Sek.Core` 32% → 97.5% (IR, DOT/Mermaid/HTML renderers, `.seexpl` round-trip, GraphAnalysis).
- `Sek.Modeling` 69% → 88.4% (Set/Sequence/Map value containers, guards, requirement capture).
- `Sek.Engine` 45.1% → 45.8% (ActionImportResolver + end-to-end exploration of an in-test model).
- Components Json branch 75% → 100%; Graphs/Random already 100%.

## Remaining gaps & the plan to green

1. **`sek` / Sek.Cli (10.2%) — architectural blocker.** The CLI logic lives in top-level
   statements in `Program.cs`, so it cannot be unit-tested in-process, and a spawned `sek.dll`
   process is not captured by the test host's coverage. **This needs a refactor (a REF): extract
   the command bodies out of `Program.cs` into a testable `Sek.Cli` library type** (which ARC001
   also wants — semantic/back-end logic must not live in the CLI). Until then the CLI cannot reach
   95% and the gate cannot pass. **This is the single biggest blocker.**
2. **`Sek.Engine` (45.8%)** — cover `BehaviorAutomaton` (NFA→DFA, product, return-binding),
   parameter generation, and the point-shoot/accept-completion composition with focused models.
3. **`Sek.Solver` (50.3%)** — cover `Z3Solver` and the remaining `Combinatorics` paths.
4. **`Sek.Cord` (71.8%)** — cover `ConstraintExtraction`/`ExprParser` edge cases and semantics.
5. **Every module still needs an `MDL` + `CRD`** (a SEK self-model + CORD exploration), not just
   unit tests, to satisfy the gate's "modelled + explored" criteria — the dogfooding the HANDOFF
   calls for.

## Deliberately uncovered (with rationale)

- None yet claimed — coverage is far below target, so no line is being waived.

## Readiness Gate verdict

- [ ] PASS
- [x] **FAIL — NOT READY.** Failing rows: every module (coverage <95% and/or no MDL/CRD). Largest
  gap: `sek`/Sek.Cli (blocked on the extract-command-bodies refactor). Next:
  `/speckit.engloopkit.model` + `/speckit.engloopkit.explore` per module, and a REF to make the
  CLI testable.

> Per PM001, SEK will not be called "ready for incidents" until this verdict is PASS.

## Related

- Gate definition: EngLoopKit v1.3.0 (PM001), `docs/standards.md` Readiness Gate
- Models/Explorations: MDL001/CRD001 (Turnstile pilot only, so far)
