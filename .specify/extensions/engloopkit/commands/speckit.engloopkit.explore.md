---
description: Stage 5 — Author CORD models against a SEK model, run Z3 exploration to enumerate behaviors, and generate fast test cases — without spending a token per test.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). It names the
model (`MDLxxx`) or the coverage gap (`COVxxx`) to target.

## Why this is token-efficient

This is the loop where EngLoopKit earns its keep. Instead of asking an LLM to imagine
test cases (expensive, coverage unverifiable), you author a CORD model and let **SEK
explore it with Z3** to enumerate behaviors and **generate** test cases from the
explored paths. Coverage becomes a proof, not a guess, and it costs solver time — not
tokens. Target tests that give **very good functional coverage but execute quickly**.

## Artifact root

Where a path below is written `docs/…`, read it as `<ARTIFACT_ROOT>/…` — the project's
artifact root (**default `docs/`**; a project whose `docs/` is a published site overrides
it, e.g. `engloop/`, in its local `standards.md`). The numbering registry is the
project's `<ARTIFACT_ROOT>/numbering-registry.md`, not the bundle's copy.

## Loop definition

- **Trigger:** a SEK model (`MDLxxx`) and, on later iterations, a coverage gap
  (`COVxxx`) to close.
- **Goal:** a CORD exploration whose generated tests are green, fast, and move coverage
  toward the Stage-5 threshold.
- **Actions:** write/extend CORD, run Z3 exploration, generate tests, run them.
- **Verification:** generated tests pass and run within the time budget; the paths
  explored match the intended scenarios.
- **Memory:** `docs/cord/CRDxxx_<slug>.md` + the `.cord` script.

## Step 0 — Assign the CRD number

Read the `CRD` "Last used" value in
[`docs/numbering-registry.md`](../../docs/numbering-registry.md); new number = +1,
zero-padded. **Increment the registry first.** Derive a `<slug>`.

## Step 1 — Choose the scenarios (Reason)

Given the target model and (if any) the coverage gap:

1. If a `COVxxx` gap drives this, read it — it names the uncovered lines/branches and
   suggests which behaviors are missing.
2. Decide the scenarios worth exploring: the paths through the model that exercise the
   uncovered behavior. Prefer a **small number of high-coverage explorations** over
   many overlapping ones (the solver will find the minimal covering set).
3. Set the exploration bounds (depth, scenario argument domains) so the run is
   **bounded and fast**. A model that doesn't converge is a modeling bug — fix bounds,
   don't remove constraints.

## Step 2 — Author the CORD model (Act)

Write the `.cord` script expressing the scenarios and constraints against the target
model. Keep comments out of constraint bodies that get split on `;` unless the CORD
parser strips them (assume the SEK convention already handles this). One CRD targets a
coherent set of related scenarios.

## Step 3 — Explore and generate (Act)

Run SEK exploration over the model with the CORD script, then generate test cases from
the explored paths. Record counts (states explored / transitions / goal states) so the
result is reproducible.

## Step 4 — Run and verify (Evaluate)

Run the generated tests. They must be **green** and **within the time budget**. If a
generated test fails, that is a real finding — either the implementation is wrong (open
an incident-worthy bug) or the model is wrong (fix the `MDLxxx`). Never delete a failing
generated test to go green.

## Step 5 — Record the CRD (Memory)

Create `docs/cord/CRD<NNN>_<slug>.md` from
[`templates/CRD-template.md`](templates/CRD-template.md): target model, scenarios,
bounds, exploration counts, generated-test location, and the coverage goal this CRD
targets.

## Step 6 — Report and hand off

```
CRD<NNN> created: docs/cord/CRD<NNN>_<slug>.md
Explored: <states>/<transitions>/<goals>   Generated tests: <n> (all green, <time>)
Next: /speckit.engloopkit.coverage to measure and decide whether to explore more.
```

## Done when

- [ ] `CRD` counter incremented
- [ ] `.cord` script authored and bounded
- [ ] Exploration run; tests generated
- [ ] Generated tests green and fast (no test deleted to pass)
- [ ] `docs/cord/CRD<NNN>_<slug>.md` created
- [ ] Handed off to coverage
