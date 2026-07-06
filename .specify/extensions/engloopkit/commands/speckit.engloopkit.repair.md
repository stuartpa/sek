---
description: Stage 6 — Route a repair item to the right-sized loop (tinyspec for small, specify for large), then make sure the SEK model, CORD explorations, and tests are updated so the fix is both shipped and permanently covered.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding. It identifies the repair item,
e.g. `PM007 RPI002`, or describes the fix directly.

## Purpose

A Repair Item is the primary output of a post-mortem. This command routes it to the
**right-sized** loop so you don't spend the tokens of a full `specify` run on a
five-line change — and, crucially, makes sure every repair also updates the model and
tests so the failure class stays covered forever.

## Artifact root

Where a path below is written `docs/…`, read it as `<ARTIFACT_ROOT>/…` — the project's
artifact root (**default `docs/`**; a project whose `docs/` is a published site overrides
it, e.g. `engloop/`, in its local `standards.md`). The numbering registry is the
project's `<ARTIFACT_ROOT>/numbering-registry.md`, not the bundle's copy.

## Loop definition

- **Trigger:** an `RPIxxx` from a post-mortem (or a direct small change request).
- **Goal:** the fix is committed to source, honors the architecture, and is
  permanently covered by generated tests.
- **Actions:** classify size → tinyspec or specify → update `MDL`/`CRD` → re-explore →
  re-check coverage.
- **Verification:** tests pass (including newly generated ones), `architecture-verify`
  clean, coverage not regressed.
- **Memory:** a tinyspec file or an `SPxxx`; updated `MDLxxx`/`CRDxxx`/`COVxxx`.

## Step 1 — Load the repair item

If `$ARGUMENTS` names a `PMxxx RPIxx`, read that post-mortem and extract the Repair
Item's description, bug class, and verification criterion. Otherwise treat the argument
as the change description.

## Step 2 — Classify size (Reason)

Use tinyspec's classifier as the router:

```
/speckit.tinyspec.classify <repair-item description>
```

Decide:
- **Small** (roughly ≤ 5 files, ≤ 8 tasks, no schema/architecture change) → **tinyspec**.
- **Large** (multiple stories, schema, new integration, cross-cutting, or architecture
  impact) → **full specify** under architecture governance.

When in doubt, prefer the smaller loop first; upgrade if it overflows tinyspec's scope
guard.

## Step 3a — Small path (tinyspec)

```
/speckit.tinyspec  <repair-item>
/speckit.tinyspec.implement
```

The single tinyspec file lives in `specs/tiny/`. Reference the originating `PMxxx
RPIxx` inside it for traceability.

## Step 3b — Large path (governed specify)

Run the full loop, governed by architecture-guard so the fix honors the architecture:

```
/speckit.architecture-guard.governed-spec  ONE-AND-DONE: <repair-item title>
/speckit.plan
/speckit.tasks
/speckit.implement
```

This produces an `SPxxx`. This is the point where the Operations loop **re-enters the
Delivery loop** (Stage 3).

## Step 4 — Update the model and tests (MANDATORY)

A repair is not done when the code compiles — it is done when the failure class is
**permanently covered**. So, for either path:

1. `/speckit.engloopkit.model` — update the affected `MDLxxx` if the repair changed the
   state space (new state, new action, changed invariant).
2. `/speckit.engloopkit.explore` — add or extend a `CRDxxx` that explores the exact
   behavior that failed, and generate tests for it. There should be a generated test
   that would have caught this bug.
3. `/speckit.engloopkit.coverage` — confirm coverage did not regress and the new
   behavior is covered.

## Step 5 — Verify against the Golden Rule

The fix is done only when it is committed to source, built into release artifacts,
deployed to the target environment, and passes all verification tests. Confirm each.

## Step 6 — Report and update the PM

```
RPI<k> repaired via <tinyspec | SP<NNN>>.
Model: MDL<..> updated   CORD: CRD<..> added   Coverage: COV<..> (no regression)
Verification: tests green, architecture-verify clean.
```

Update the post-mortem's Repair Items section with the resulting spec/tinyspec path and
mark the RPI done.

## Done when

- [ ] Repair item classified (tinyspec vs specify) and implemented on that path
- [ ] Fix honors the architecture (governed / architecture-verify clean)
- [ ] `MDL`/`CRD` updated; a generated test now covers the failed behavior
- [ ] Coverage not regressed
- [ ] Golden Rule satisfied (in source, built, deployed, verified)
- [ ] Post-mortem RPI marked done with a link to the spec/tinyspec
