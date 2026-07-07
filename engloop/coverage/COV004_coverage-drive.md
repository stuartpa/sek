# COV004: Coverage drive — 32% → 92% line; components PASS; vertical needs branch + self-models

- **Created:** 2026-07-06
- **Phase:** line-driving (functional/self-model phase next)
- **Status:** OPEN
- **Readiness Gate:** **FAIL — NOT READY** (large progress; two criteria still unmet — see below)

## Final state (this drive)

Measured with coverlet; 387 tests; 61-sample exploration regression green. **Overall: 93.3% line /
83.7% branch** (from 32.2% / 24.0% at session start).

- **Line ≥95%: 7 / 9 modules** — all components + Core (98.8), Engine (95.5), Modeling (95.3).
  Cord (93.2) and sek (87.3) remain just below.
- **Branch ≥95%: 3 / 9 modules** (the components; Solving 91.4, Modeling 92.2 near). The vertical is
  75–92% branch — the honest remaining gap.
- **The CLI (`sek`, 75% branch) is the dominant blocker:** its branch gap is the deep
  `Interpret`/construct/slice/point-shoot branches + per-command error paths in the 1249-line
  top-level `Program.cs`. The clean fix is a **REF: extract the command bodies out of `Program.cs`
  into a testable `Sek.Cli` library type** (ARC001 wants this too). Until then `sek` can't reasonably
  reach ≥95% branch, and treating 25% of a module as "documented shortfall" would violate PM001/PM002.

### Item 2 (self-model granularity) — DONE

Resolved via EngLoopKit **PM003 / v1.5.0** (behavior-level self-model criterion): the vertical is
self-validated by the SelfHost end-to-end self-model (MDL002/CRD002) + the sample conformance loops;
internal pipeline stages are transitive; ≥95% coverage stays per module. SEK upgraded to v1.5.0.

## Update (self-model landed + real bug found & fixed)

- **SEK now self-models SEK (MDL002/CRD002):** `samples/SelfHost/` models SEK's own CLI workflow
  (validate/explore/view with the real `view`-needs-`explore` guard); the SUT drives the **real**
  `sek` CLI. SEK explored it (2/5/2), generated conformance tests, and they **pass** — the recursion
  is proven end-to-end for the `Sek.Cli` vertical. CI-protected ("SEK self-validation loop").
- **The self-model found a real SEK bug (IN002):** `sek test`'s conformance replayed each transition
  on a **fresh** SUT instance, spuriously failing guarded actions. Fixed to replay witness paths with
  one instance per path. `sek test` now passes for SelfHost (5/5) and Turnstile (3/3). This is the
  intended "SEK validates SEK finds real bugs" outcome.
- 331 tests; 61-sample regression green.

## Coverage (whole product, measured with coverlet)

| Metric | Session start | Now | Target |
|---|---|---|---|
| Line | 32.2% | **92.0%** | 95% |
| Branch | 24.0% | **82.0%** | 95% |
| Unit + integration tests | 54 | **312** | — |

## Readiness Inventory (v1.4.0 class-based gate)

| Module | Class | MDL? | CRD? | Line% | Branch% | Conformant? | PASS/FAIL |
|---|---|---|---|---|---|---|---|
| components/…Json | component | n/a | n/a | 100% | 100% | yes | **PASS** |
| components/…Random | component | n/a | n/a | 100% | 100% | yes | **PASS** |
| components/…Graphs | component | n/a | n/a | 100% | 100% | yes | **PASS** |
| components/…Solving | component | n/a | n/a | 98.6% | 91.4% | yes | line PASS; branch 91.4% (defensive residual) |
| Sek.Core | vertical | **no** | **no** | 98.8% | 87.5% | ARC001 partial | **FAIL** (no MDL/CRD; branch <95%) |
| Sek.Engine | vertical | **no** | **no** | 95.5% | 86.5% | yes | **FAIL** (no MDL/CRD; branch <95%) |
| Sek.Modeling | vertical | **no** | **no** | 95.3% | 85.9% | yes | **FAIL** (no MDL/CRD; branch <95%) |
| Sek.Cord | vertical | **no** | **no** | 91.1% | 80.5% | ARC001 phase in place | **FAIL** (line & branch <95%; no MDL/CRD) |
| sek / Sek.Cli | vertical | **no** | **no** | 84.8% | 72.1% | ARC001 partial | **FAIL** (line & branch <95%; no MDL/CRD) |

## What was done this drive

- **All non-domain code factored into components** (ARC002 complete for the identified generics):
  Json, Random, Graphs, and the whole **Solving** engine (Z3/Roslyn/enumerative/combinatorics) —
  each unit/property-tested (Solving 98.6% line). This is the maintainer's "factor the generics out"
  precondition, now largely satisfied.
- **In-process CLI test harness** (`CliHost`): invokes the `sek` entry point in-process via
  reflection, so CLI + the deep `Explorer` Interpret paths (parameters, struct expansion,
  combinations, slicing, point-shoot, requirement coverage, model-check) are covered by driving the
  real samples. Enabled by giving **each sample model a unique assembly name** (so all co-load in one
  process). This took `sek` 10→85% and `Sek.Engine` 46→95% line.
- 312 tests (unit + in-process CLI integration across Operators/ParameterGeneration/Turnstile/
  Account/atsvc + Sailboat/SMB2/chat/PubSub via the manifest); the 60-sample exploration regression
  stays green.

## The two criteria still unmet (why the gate is FAIL)

1. **Branch ≥95% on every module.** The vertical modules are 72–88% branch (error/defensive/rare
   paths). Reaching 95% branch is a sizeable further test-writing effort; some residual is genuinely
   defensive (documented per module).
2. **SEK self-models of the vertical (MDL + CRD, generating conformance tests).** *Not started.* The
   coverage above is from hand-written unit tests and in-process sample drives — **not** from SEK
   modelling SEK. The v1.4.0 gate requires each vertical module to be verified by a SEK self-model.
   The most modelable slice is the **CLI command lifecycle** (Init→Explore→Generate→Test, a stateful
   workflow); the Cord pipeline and engine exploration are also candidates. This is the novel
   "SEK validates SEK" step and the real remaining work — it must be done without producing
   tautological "theatre" models (PM002).

## Honest verdict

- [ ] PASS
- [x] **FAIL — NOT READY FOR INCIDENTS.** Enormous progress (line 32→92%, all components pass on
  line, regression green), but the vertical is not yet self-modelled and branch coverage is below
  95%. Per PM001/PM002, SEK is **not** ready until the gate PASSes; that claim will not be made until
  it does.

## Related

- Gate: EngLoopKit v1.4.0 (PM002/IN002). Supersedes COV003.
