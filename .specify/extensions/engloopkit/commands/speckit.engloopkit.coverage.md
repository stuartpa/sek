---
description: Stage 5 — Measure WHOLE-PRODUCT code coverage, close the SEK-to-coverage loop to 95%+ line/branch then functional, and compute the Readiness Gate that is the hard precondition for Stage 6 (operate). "Ready for incidents" is the OUTPUT of this gate, never a narrated claim.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## The loop this closes

`explore` generates tests from CORD models; `coverage` measures what those tests
actually cover and decides whether to explore more. Together they are the inner
**Verification loop** (Observe coverage → Reason about the gap → Act by extending a
CORD exploration → Evaluate new coverage → repeat). The exit condition is a real,
verifiable number — not an LLM's opinion.

## Artifact root

Where a path below is written `docs/…`, read it as `<ARTIFACT_ROOT>/…` — the project's
artifact root (**default `docs/`**; a project whose `docs/` is a published site overrides
it, e.g. `engloop/`, in its local `standards.md`). The numbering registry is the
project's `<ARTIFACT_ROOT>/numbering-registry.md`, not the bundle's copy.

## Loop definition

- **Trigger:** generated tests exist (from one or more `CRDxxx`).
- **Goal (ordered):** (1) **95%+ line/branch coverage across the WHOLE product** — every
  module (each `components/*` and the vertical), not just whatever tests happen to exist; then
  (2) **functional coverage** — the behaviors that matter are exercised even where line coverage
  is already satisfied. All tests green and within the time budget throughout.
- **Actions:** run the test suite under real coverage tooling, build the whole-product Readiness
  Inventory, diff against the goal, point `explore` at the largest remaining gap.
- **Verification:** the **Readiness Gate** (Step 3.5) returns **PASS** — every module modelled
  (`MDL`), explored (`CRD`), covered ≥95% line & branch, architecture-conformant, gates green.
- **Memory:** `docs/coverage/COVxxx_<slug>.md`.

## The Readiness Gate (Definition of Ready-for-Incidents)

> **"Ready for incidents" / "ready to operate" is the OUTPUT of this gate — never an input the
> agent asserts.** A project may NOT enter Stage 6 (operate) until this gate returns **PASS**.
> No command and no agent may state a project is ready on the basis of stage completion, a pilot,
> or apparent doneness. If the gate has not been computed and PASSed, the honest status is
> **NOT READY**, and that is the only statement allowed. (See PM001.)

The gate **PASSES** iff, for **every** module of the product (each `components/*` component **and**
the vertical), ALL of the following are true — proven by evidence, not opinion:

1. **Modelled** — the module has an `MDL` (a SEK model of its behavior).
2. **Explored** — the module has a `CRD` (a CORD exploration that generated or drove its tests).
3. **Covered** — **measured** line coverage ≥95% **and** branch coverage ≥95% (from real
   coverage tooling output, attached), or every shortfall line is listed with a rationale.
4. **Architecture-conformant** — the module honors every applicable `ARC` / architecture-guard
   check (no boundary violations, no leaked components).
5. **Green** — the full unit-test suite and any exploration-regression gate pass.

If **any** module fails **any** criterion, the gate is **FAIL** and the product is **NOT READY**.

## Step 0 — Assign the COV number

Read the `COV` "Last used" value in
[`docs/numbering-registry.md`](../../docs/numbering-registry.md); new number = +1,
zero-padded. **Increment the registry first.**

## Step 1 — Measure the WHOLE product (Observe)

Run the **entire** test suite under **real coverage tooling** (e.g. coverlet /
`dotnet test --collect:"XPlat Code Coverage"`, `go test -cover`, `coverage.py`, etc. — whatever
is idiomatic for the stack). Do **not** estimate. Record, **per module** (each `components/*` and
the vertical) and overall:
- line and branch coverage,
- whether the module has an `MDL` and a `CRD`,
- total suite runtime (the "fast" constraint) and any slow outliers.

Build the **Readiness Inventory** — one row per module of the product:

| Module | MDL? | CRD? | Line% | Branch% | Conformant? | PASS/FAIL |
|---|---|---|---|---|---|---|
| components/<Name> | | | | | | |
| <vertical> | | | | | | |

A module with **no tests at all** is `Line 0% / FAIL` — it does not get to be absent from the
table. The product's module list comes from the repo (every `components/*` project and every
vertical assembly), not from "which ones happen to have a model."

## Step 2 — Find the gap (Reason)

1. **Phase 1 — line/branch to 95%+.** List the files/branches below target, largest
   gaps first. For each, identify the behavior that would cover it.
2. **Phase 2 — functional coverage (only once Phase 1 is met).** Ask what *behaviors*
   still lack a test even though their lines are hit — error paths, boundary values,
   concurrency interleavings, invariants. These are the models to add next.
3. Watch the time budget: if coverage is met but the suite is slow, the fix is a
   tighter CORD exploration (fewer, higher-coverage paths), not more tests.

## Step 3 — Act: drive the loop

- If the Goal is **not** met: write a `COVxxx` naming the specific gaps, then hand back
  to `/speckit.engloopkit.explore` pointed at the largest gap. Repeat.
- If the Goal **is** met: record the final `COVxxx` and declare the Verification loop
  complete.

Do not chase 100% blindly — after 95%+ line/branch, prioritize functional coverage of
behaviors that matter over the last few unreachable/trivial lines. Mark deliberately
uncovered lines with a rationale in the COV doc.

## Step 3.5 — Compute the Readiness Gate (Evaluate)

Walk the Readiness Inventory. The gate is **PASS** only if **every** row satisfies all five gate
criteria (Modelled, Explored, Covered ≥95% line & branch, Conformant, Green). Otherwise it is
**FAIL**. Record the verdict and, if FAIL, the exact failing rows and why.

**This verdict is the only source of a "ready" statement.** If FAIL, the product is NOT READY and
the loop continues (hand the largest gap back to `explore`/`model`). Do not soften, round up, or
narrate around a FAIL.

## Step 4 — Record the COV (Memory)

Create `docs/coverage/COV<NNN>_<slug>.md` from
[`templates/COV-template.md`](templates/COV-template.md): coverage numbers (before/
after), suite runtime, remaining gaps with rationale, and which `CRDxxx` to extend next
(or "loop complete").

## Step 5 — Report

Always attach the **Readiness Inventory** table and the **gate verdict**. Use exactly one of the
two templates below — the choice is dictated by the gate, not by judgement.

**Gate FAIL (the common case while building out):**

```
COV<NNN>: line <x>% / branch <y>% (whole product)   suite <time>
Readiness Gate: FAIL — NOT READY FOR INCIDENTS
Failing modules: <module: reason> (e.g. "vertical: no MDL/CRD, line 12%"; "components/Foo: branch 88%")
Phase: <line-driving | functional>
Next: /speckit.engloopkit.model or /speckit.engloopkit.explore on the largest gap.
```

**Gate PASS (only when EVERY inventory row passes):**

```
COV<NNN>: line <x>% / branch <y>% (whole product, every module ≥95%)   suite <time>
Readiness Gate: PASS
Every module: modelled + explored + covered ≥95% line & branch + architecture-conformant + green.
The product is implemented, governed, modelled, explored, and covered. READY FOR INCIDENTS.
```

> **Anti-narration rule (PM001):** you may emit the PASS template ONLY after Step 3.5 computed PASS
> against a complete Readiness Inventory backed by real coverage-tool output. "Ready for incidents"
> stated any other way — from stage completion, a pilot, or a feeling of doneness — is the exact
> defect PM001 exists to prevent. When in doubt, the status is NOT READY.

## Done when

- [ ] `COV` counter incremented
- [ ] WHOLE-PRODUCT coverage measured with real tooling (line/branch + runtime), per module
- [ ] Readiness Inventory built (one row per `components/*` + the vertical; no module omitted)
- [ ] Readiness Gate computed (Step 3.5): explicit PASS or FAIL with failing rows named
- [ ] Gaps identified and either closed (via model/explore) or justified per line
- [ ] `docs/coverage/COV<NNN>_<slug>.md` created with the inventory + verdict attached
- [ ] A "ready for incidents" statement made ONLY if the gate PASSED; otherwise reported NOT READY
