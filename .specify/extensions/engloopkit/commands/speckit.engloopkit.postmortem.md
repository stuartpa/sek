---
description: Stage 6 — Analyze a set of stabilized incidents. Run 5-whys, extract class-level Learnings, design ONE-AND-DONE Repair Items, and write a numbered PM document that feeds the repair loop.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). It should point
at one or more incident documents (`INxxx`) to analyze together.

## Context requirements

You **MUST** have access to:
1. the incident document(s) from `docs/incidents/INxxx_<slug>.md`,
2. the architecture (`ARCxxx` and architecture-guard constitutions),
3. the release model (how code reaches the target environment).

If an incident document path is not provided, **ask the user** which incidents this
post-mortem covers. A post-mortem may cover a **set** of related incidents — that is the
intended granularity.

## Readiness precondition (PM001)

> "Ready for incidents" / "ready to operate" is the **verdict of the Readiness Gate**
> (`/speckit.engloopkit.coverage`, Step 3.5), never a narrated claim. A repair item that would
> let any command or agent assert readiness *without* a PASSing whole-product gate re-introduces
> the PM001 defect — do not design one.

## Artifact root

Where a path below is written `docs/…`, read it as `<ARTIFACT_ROOT>/…` — the project's
artifact root (**default `docs/`**; a project whose `docs/` is a published site overrides
it, e.g. `engloop/`, in its local `standards.md`). The numbering registry is the
project's `<ARTIFACT_ROOT>/numbering-registry.md`, not the bundle's copy.

## Loop definition

- **Trigger:** a set of incidents is stable and chosen for analysis.
- **Goal:** class-level Learnings and concrete, shippable Repair Items that make the
  failure class mechanically impossible.
- **Actions:** 5-whys, ONE-AND-DONE design, write the PM, hand each RPI to the repair
  loop.
- **Verification:** every Repair Item is specific enough to hand directly to
  `/speckit.engloopkit.repair`.
- **Memory:** `docs/postmortems/PMxxx_<slug>.md`; indexed in `docs/postmortems/INDEX.md`.

## Step 1 — Extract incident context (Observe)

Read each incident document. Extract: start/duration, affected components, mitigations
applied (`MIT`), cause-class, and system state. Map the affected components to the
architecture.

## Step 2 — Five whys (Reason)

For the set (or per incident where causes differ), state the symptom and ask "why?"
five times, drilling to the systemic level. Record the full chain.

## Step 3 — ONE-AND-DONE structural fixes

For each root cause, abstract from the concrete bug to its **class**, then design a fix
that makes the **entire class** mechanically impossible:

- **Concrete bug:** the specific thing that broke.
- **Bug class:** the general failure it is an instance of.
- **Structural fix:** mechanical/automated (never "be more careful"), class-preventing,
  and **verifiable**.

## Step 4 — Number Learnings and Repair Items

Within this post-mortem, number sequentially (local counters):
- **Learnings** `LRN001`, `LRN002`, … — class-level insights ("config can silently
  diverge from source"), never instance-level.
- **Repair Items** `RPI001`, `RPI002`, … — the shippable structural fixes. Each RPI
  must be specific enough to hand to `/speckit.engloopkit.repair`, and belongs in
  source (not as a live patch).

## Step 5 — Write the PM (Memory)

1. Read the `PM` "Last used" value in
   [`docs/numbering-registry.md`](../../docs/numbering-registry.md); new number = +1,
   zero-padded. **Increment the registry first.**
2. Create `docs/postmortems/PM<NNN>_<slug>.md` from
   [`templates/PM-template.md`](templates/PM-template.md): timeline (condensed from the
   incidents), root causes, 5-whys, ONE-AND-DONE analysis, Learnings, Repair Items,
   cause-class tags, references to the `INxxx` covered.
3. Update `docs/postmortems/INDEX.md` (create it if absent) with a row linking the PM,
   its cause-class tags, and whether it is a recurrence of a prior PM.

## Step 6 — Hand off to repair

For each Repair Item, hand off to the repair router:

```
/speckit.engloopkit.repair PM<NNN> RPI<k>
```

Update the PM's Repair Items section with the resulting `SPxxx` or tinyspec path as they
are created.

## Step 7 — Report

```
Post-mortem PM<NNN>: docs/postmortems/PM<NNN>_<slug>.md
Covers: IN<..>   Learnings: LRN001..   Repair Items: RPI001..
Next: /speckit.engloopkit.repair for each RPI.
```

## Done when

- [ ] Incident(s) read and mapped to architecture
- [ ] 5-whys completed to the systemic level
- [ ] ONE-AND-DONE fixes designed (mechanical, class-preventing, verifiable)
- [ ] `LRN` and `RPI` numbered within the PM
- [ ] `PM` counter incremented; `docs/postmortems/PM<NNN>_<slug>.md` created; INDEX updated
- [ ] Each RPI handed to the repair loop
