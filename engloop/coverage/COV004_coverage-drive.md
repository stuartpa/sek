# COV004: Coverage drive — 32% → 92% line; components PASS; vertical needs branch + self-models

- **Created:** 2026-07-06
- **Phase:** line-driving (functional/self-model phase next)
- **Status:** OPEN
- **Readiness Gate:** **FAIL — NOT READY** (large progress; two criteria still unmet — see below)

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
