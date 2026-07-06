---
description: Stage 6 — Live incident response. Stabilize the system fast with mitigations (not permanent fixes), log everything to a numbered IN document, and hand off to the post-mortem once verified stable.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). It describes the
symptom.

## The Golden Rule

> **A patch applied during an incident is a Mitigation (MIT), not a fix.**
> Restore service first. Permanent fixes come later, through the post-mortem → repair →
> specify loop. Do **not** commit a permanent fix during an incident.

## Readiness precondition (PM001)

> A project only *reaches* the operate stage after the **Readiness Gate** passes
> (`/speckit.engloopkit.coverage` — every module modelled + explored + ≥95% line/branch +
> conformant + green). "Ready for incidents" is the gate's verdict, **never** a narrated claim
> from stage completion or a pilot. If you are tempted to say a project is ready without a PASSing
> gate, that is the PM001 defect — the honest status is NOT READY.

## Artifact root

Where a path below is written `docs/…`, read it as `<ARTIFACT_ROOT>/…` — the project's
artifact root (**default `docs/`**; a project whose `docs/` is a published site overrides
it, e.g. `engloop/`, in its local `standards.md`). The numbering registry is the
project's `<ARTIFACT_ROOT>/numbering-registry.md`, not the bundle's copy.

## Loop definition

- **Trigger:** a bug reported by a user or surfaced by monitoring.
- **Goal:** the system is stable and users are unblocked — **not** root cause fixed.
- **Actions:** triage, apply mitigations, log each one, verify recovery.
- **Verification:** health checks pass; user workflows unblocked (or failing only for
  understood, documented reasons).
- **Memory:** `docs/incidents/INxxx_<slug>.md` with a `MIT`-numbered timeline.

## Step 0 — Create the incident document (MANDATORY, before any diagnosis)

1. Read the `IN` "Last used" value in
   [`docs/numbering-registry.md`](../../docs/numbering-registry.md); new number = +1,
   zero-padded. **Increment the registry first.**
2. Derive a 2–3 word `<slug>` from the symptom.
3. Create `docs/incidents/IN<NNN>_<slug>.md` from
   [`templates/IN-template.md`](templates/IN-template.md). This document is your
   **single source of truth** — every action is logged here as it happens.
4. Set `Status: INVESTIGATING`.

## Step 1 — Triage (Observe)

1. Collect symptom details: observed failure, when first noticed, last known-good
   state, scope (how many users/systems).
2. Check current state with whatever tools you have (logs, process/service status,
   recent changes, connectivity, data layer). Stay generic — no assumption about the
   stack.
3. Log findings in the timeline as you go.
4. Form a hypothesis about which layer is affected.

## Step 2 — Stabilize (Act — Mitigations)

Do **not** fix root causes yet. Restore service with mitigations, numbering each
`MIT001`, `MIT002`, … **within this incident**:

- restart / fail over / scale up / roll back the recent change / apply a circuit-breaker
  workaround.

For each mitigation: log the action and its evidence in the timeline, then smoke-test.
Continue until health checks pass and user workflows are unblocked. Change
`Status: INVESTIGATING → STABILIZED`.

## Step 3 — Diagnose enough to hand off (Reason)

Once stable, dig only far enough to classify the cause and write a good hand-off — the
deep analysis is the post-mortem's job. Classify the cause-class (e.g. state-drift,
dependency-failure, resource-exhaustion, bug-regression, deployment-incomplete,
process-gap) and record it.

## Step 4 — Verify and close (Evaluate)

1. Smoke-test the full user workflow.
2. Watch for fresh errors for a reasonable window; confirm no cascading failures.
3. Change `Status: STABILIZED → RESOLVED`; fill `Resolved at` and `Duration`.
4. Complete the **Hand-off to Post-Mortem** section: snapshot bundle location, affected
   operations, preliminary cause-class, and a suggested PM title.

## Step 5 — Report

```
Incident IN<NNN> stable and documented: docs/incidents/IN<NNN>_<slug>.md
Mitigations applied: MIT001..MIT<k>   Cause-class (preliminary): <class>
Next: once a SET of incidents is stable, run /speckit.engloopkit.postmortem.
```

## Incident-document update pattern

After every action, append a timeline row:

```
| HH:MM | <action> | <evidence/result> |
```

Do this for **every** mitigation and finding — the incident document is your live audit
trail. Nothing loops silently.

## Done when

- [ ] `IN` counter incremented; `docs/incidents/IN<NNN>_<slug>.md` created
- [ ] `Status` reached `STABILIZED` then `RESOLVED`
- [ ] Every mitigation logged as `MIT` in the timeline (no permanent fix committed)
- [ ] Cause-class recorded; system verified stable
- [ ] Hand-off section complete; post-mortem suggested
