# Operations Standards

**Status:** Ratified 2026-06-24  
**Authority:** Platform Constitution §XII — Incident-driven change

This document defines the canonical nouns, numbering, and workflow for all live-site operations. Every agent, operator, and process MUST use these definitions without deviation.

---

## Noun Definitions

### Incident — `IN001`, `IN002`, …

An unplanned disruption to the live-site that required operator or agent intervention.

- Captured in `docs/incidents/IN001_<slug>.md`
- Contains: symptom, timeline, mitigations applied, root cause hypotheses
- Does **not** contain repair items — those belong in the Post-Mortem
- Closed when the live-site is stable; not when the root cause is fixed
- Two incidents on the same day may be numbered separately (`IN001`, `IN002`) if they have independent root causes

### Mitigation — `MIT001`, `MIT002`, …

A live-site action applied during an incident to restore service. Mitigations are **not fixes** — they are temporary stabilisations (kubectl patches, manual API calls, pod restarts, etc.).

- Numbered sequentially within an incident: `MIT001`, `MIT002`, …
- Recorded in the incident timeline table
- Every mitigation must be superseded by a Repair Item before the Post-Mortem is closed

### Post-Mortem — `PM001`, `PM002`, …

A structured analysis of one or more incidents, written after the live-site is stable.

- Captured in `docs/postmortems/PM-NNN_<slug>.md` and indexed in `POSTMORTEM_INDEX.md`
- Contains: timeline, 5-whys root causes, ONE AND DONE analysis, Learnings, Repair Items
- References the incident(s) it covers by `IN` number
- Closed only when all Repair Items are verified on live-site

### Learning — `LRN001`, `LRN002`, …

A structural insight extracted from a Post-Mortem. Learnings describe *why* a class of failure happens, not just the specific instance.

- Numbered sequentially within a Post-Mortem: `LRN001`, `LRN002`, …
- Must be written at the class level ("DAB configmap can silently diverge from source") not the instance level ("dab-config had 62 entities")
- Each Learning drives one or more Repair Items

### Repair Item — `RPI001`, `RPI002`, …

A concrete, shippable fix that prevents a class of failure from recurring. Repair Items are the **primary output** of every Post-Mortem.

- Numbered sequentially within a Post-Mortem: `RPI001`, `RPI002`, …
- Must be specific enough to hand directly to `speckit.specify`
- Belongs in  source — not a patch to a live machine/instance, not a livesite only change
- Not done until it passes live-site validation tests

### Live-Site

The physical running site. All mitigations are applied here. All Repair Items must ultimately be validated here.

---

## Incident → Post-Mortem → Repair Workflow

```
┌──────────────────────────────────────────────────────────────┐
│  INCIDENT (INxxx)                                            │
│  Something breaks on Live-Site                               │
│                                                              │
│  Apply Mitigations (MITxxx) to stabilise Live-Site          │
│  Document in docs/incidents/INxxx_<slug>.md                  │
└──────────────────────────┬───────────────────────────────────┘
                           │ Live-Site stable
                           ▼
┌──────────────────────────────────────────────────────────────┐
│  POST-MORTEM (PMxxx)                                         │
│  Written after Live-Site is stable                           │
│                                                              │
│  Learnings (LRNxxx): why did this class of failure happen?   │
│  Repair Items (RPIxxx): what structural fix prevents it?      │
└──────────────────────────┬───────────────────────────────────┘
                           │ For each RPI
                           ▼
┌──────────────────────────────────────────────────────────────┐
│  SPECKIT PIPELINE                                            │
│  speckit.specify → Spec (SPxxx)                              │
│  speckit.plan → plan.md                                      │
│  speckit.tasks → tasks.md                                    │
│  speckit.implement → code committed to repo             │
└──────────────────────────┬───────────────────────────────────┘
                           │ Code merged to repo
                           ▼
┌──────────────────────────────────────────────────────────────┐
│  RELEASE PIPELINE                                            │
│  1. Rebuild all affected build output from repo        │
│  2. Push new image tags to registry            │
│  3. Update helm chart image references                       │
│  4. helm upgrade on Live-Site 
│  5. Run validation tests — ALL must pass                     │
│  6. If any test fails → helm rollback immediately            │
└──────────────────────────────────────────────────────────────┘
```

---

## The Golden Rule

> **A patch during an incident is a Mitigation (MIT), not a fix.**
> A fix is not done until it is committed to the repo, built into release artifacts, deployed to Live-Site, and has passed all validation tests.

---

## Numbering Registry

Counters are maintained here. Increment before creating a new document.

| Counter | Last used | Notes |
|---|---|---|
| `IN` | `IN002` | IN001 = machine down; IN002 = bad config |
| `MIT` | `MIT014` | MIT001–MIT007 in IN001; MIT008–MIT014 in IN002 |
| `PM` | `PM-002` | PM-001 = unreliable handling of bad device; PM-002 = constitution violation cascade |
| `LRN` | — | Assigned per-PM; reset per document |
| `RPI` | — | Assigned per-PM; reset per document |
| `SP` | `SP001` | Single consolidated platform spec |

---

---

## Validation Test Gate (post-deployment)

Every Live-Site deployment must pass all tests listed in the relevant incident/postmortem before the incident can be closed.