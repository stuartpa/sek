---
description: Stage 4 — Build a SEK model of the implementation's state space (state fields, actions, invariants) as the substrate for Z3 exploration and test generation.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). It names the
component or subsystem to model.

## What SEK is

SEK is the workspace's Z3-backed model explorer and test generator. A SEK **model**
abstracts an implementation into an explorable state space: **state fields**, **actions**
that transition between states, and **invariants** that must always hold. The model is
*structural* — it answers "what states exist and how do actions move between them?" —
and it is the substrate the exploration stage (Stage 5) runs CORD models against.

This command builds the model. It is deliberately separate from `explore` (which
defines coverage-driven CORD scenarios): the model changes rarely (only when the
implementation's shape changes), while explorations change often as coverage gaps are
found. See *Why model and explore are two commands, one loop* in
[the engineering loop](../../docs/engineering-loop.md).

## Artifact root

Where a path below is written `docs/…`, read it as `<ARTIFACT_ROOT>/…` — the project's
artifact root (**default `docs/`**; a project whose `docs/` is a published site overrides
it, e.g. `engloop/`, in its local `standards.md`). The numbering registry is the
project's `<ARTIFACT_ROOT>/numbering-registry.md`, not the bundle's copy.

## Loop definition

- **Trigger:** final-form code exists (Stage 3), or code changed via a repair.
- **Goal:** a SEK model that faithfully abstracts the implementation's behavior.
- **Actions:** read the implementation, identify state/actions/invariants, write the
  model, run sanity explorations.
- **Verification:** the model builds and its sanity explorations reproduce known-good
  behavior.
- **Memory:** `docs/models/MDLxxx_<slug>.md` + the SEK model source it points to.

## Step 0 — Assign the MDL number

Read the `MDL` "Last used" value in
[`docs/numbering-registry.md`](../../docs/numbering-registry.md); the new number is
that + 1, zero-padded. **Increment the registry first.** Derive a `<slug>`.

## Step 1 — Understand the implementation (Observe)

1. Read the code for the component under `$ARGUMENTS`. Identify:
   - **State fields** — the variables that define "where" the system is (queues,
     flags, counters, connection status, containers/collections).
   - **Actions** — the operations that change state (public methods, message handlers,
     API endpoints), and for each: preconditions, effects, and result kind
     (call/return/event).
   - **Invariants** — properties that must always hold (no negative balance, a closed
     handle is never read, etc.).
2. Note where the honest abstraction boundary is: model behavior, not implementation
   detail. Prefer the smallest model that still distinguishes the behaviors tests must
   cover.

## Step 2 — Write the model (Act)

Author the SEK model source (the modeling constructs the SEK engine consumes — state
fields, actions with pre/effect, and invariants). Keep it minimal and faithful. Where
the implementation uses collections, model them as state fields (containers), not as
unbounded free variables, so exploration stays bounded.

## Step 3 — Sanity-explore (Evaluate)

Run a few small explorations to confirm the model reproduces known-good behavior:
reachable "happy path" states appear, and known-bad states are unreachable. If the
model contradicts reality, fix the model — do **not** loosen an invariant to make an
exploration pass.

## Step 4 — Record the MDL (Memory)

Create `docs/models/MDL<NNN>_<slug>.md` from
[`templates/MDL-template.md`](templates/MDL-template.md): the state fields, actions,
invariants, the abstraction choices made (and why), and a pointer to the model source.

## Step 5 — Report

```
MDL<NNN> created: docs/models/MDL<NNN>_<slug>.md
Model source: <path>
Sanity explorations: <n> pass
Next: /speckit.engloopkit.explore to author CORD models and generate tests.
```

## Done when

- [ ] `MDL` counter incremented
- [ ] Model source written (minimal, faithful, bounded)
- [ ] Sanity explorations reproduce known-good behavior
- [ ] `docs/models/MDL<NNN>_<slug>.md` created
- [ ] Next step (explore) suggested
