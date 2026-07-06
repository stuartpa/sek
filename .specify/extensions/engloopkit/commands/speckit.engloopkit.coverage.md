---
description: Stage 5 — Measure code coverage of the generated tests and close the SEK-to-coverage loop, driving line/branch coverage to 95%+ first, then functional coverage.
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
- **Goal (ordered):** (1) **95%+ line/branch coverage**; then (2) **functional
  coverage** — the behaviors that matter are exercised even where line coverage is
  already satisfied. All tests green and within the time budget throughout.
- **Actions:** run the test suite under coverage, diff against the goal, point
  `explore` at the largest remaining gap.
- **Verification:** coverage thresholds met; suite green and fast.
- **Memory:** `docs/coverage/COVxxx_<slug>.md`.

## Step 0 — Assign the COV number

Read the `COV` "Last used" value in
[`docs/numbering-registry.md`](../../docs/numbering-registry.md); new number = +1,
zero-padded. **Increment the registry first.**

## Step 1 — Measure (Observe)

Run the test suite under coverage. Record:
- line and branch coverage overall and per file,
- total suite runtime (the "fast" constraint),
- any tests that are slow outliers.

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

## Step 4 — Record the COV (Memory)

Create `docs/coverage/COV<NNN>_<slug>.md` from
[`templates/COV-template.md`](templates/COV-template.md): coverage numbers (before/
after), suite runtime, remaining gaps with rationale, and which `CRDxxx` to extend next
(or "loop complete").

## Step 5 — Report

```
COV<NNN>: line <x>% / branch <y>%   suite <time>
Phase: <line-driving | functional | COMPLETE>
Largest remaining gap: <file/behavior>  →  next: /speckit.engloopkit.explore
```

If complete:

```
COV<NNN>: coverage goal met (line <x>%, functional behaviors covered), suite <time>.
The product is implemented, governed, modeled, explored, and covered. Ready to operate.
```

## Done when

- [ ] `COV` counter incremented
- [ ] Coverage measured (line/branch + runtime)
- [ ] Gaps identified and either closed (via explore) or justified
- [ ] `docs/coverage/COV<NNN>_<slug>.md` created
- [ ] Either handed back to explore, or Verification loop declared complete
