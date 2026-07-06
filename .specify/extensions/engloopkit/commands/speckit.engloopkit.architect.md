---
description: Stage 2 — Derive the long-lived architecture from the working bridging code using architecture-guard, and record it as a numbered ARC decision that governs every later loop.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Prerequisite

The bridging code exists and runs (Stage 1 complete). The
[architecture-guard](https://github.com/DyanGalih/spec-kit-architecture-guard)
extension is installed (the `engloopkit` bundle pulls it in). This command
**orchestrates** architecture-guard; it does not replace it.

## Artifact root

Where a path below is written `docs/…`, read it as `<ARTIFACT_ROOT>/…` — the project's
artifact root (**default `docs/`**; a project whose `docs/` is a published site overrides
it, e.g. `engloop/`, in its local `standards.md`). The numbering registry is the
project's `<ARTIFACT_ROOT>/numbering-registry.md`, not the bundle's copy.

## Loop definition

- **Trigger:** bridging code exists and runs.
- **Goal:** an explicit, governed architecture — architecture-guard constitutions plus
  one or more human-readable `ARCxxx` decisions — that all later loops honor, **including
  the component boundary** (vertical code vs reusable components).
- **Actions:** run architecture-guard's brownfield mapping and workflow; capture
  boundaries, ownership, and contracts.
- **Verification:** `architecture-review` runs clean, or only with accepted, tracked
  exceptions.
- **Memory:** `docs/architecture/ARCxxx_<slug>.md`.

## Step 1 — Map the existing code (Observe)

Run architecture-guard's brownfield mapping so governance starts from reality, not
assumptions:

```
/speckit.architecture-guard.init-brownfield
```

Read what it produces. Identify the natural boundaries the bridging code already hints
at, the ownership of each module, and the contracts between them.

## Step 1b — Establish the component boundary (MANDATORY)

Enforce the **component pattern** — non-vertical code lives as components, the vertical
composes them. See [../../docs/component-pattern.md](../../docs/component-pattern.md).

1. **Classify** every module from Step 1 with the litmus test — *would this code be useful,
   unchanged, in a repo solving a totally different problem?* Yes → **component**; No →
   **vertical**.
2. **Create the components folder** in the language's idiom (C# → `components/` with a
   class-library project `<Repo>.Components.<Name>` per component; Go → `internal/<name>`;
   see the doc's language table). The vertical stays in its own folder (e.g. `src/`).
3. **Record the boundary as a governed `ARC` rule** (Step 3) that architecture-guard
   enforces: *components carry no domain knowledge; dependencies point vertical →
   components, never the reverse.*
4. **File the gap as refactor tasks**: any non-vertical code still inside the vertical is a
   violation to be resolved in Stage 3 and tightened by `refactor-scan`. Do not extract it
   all now — record it as the target the refactor stages converge toward.

This is what *causes a repo adopting EngLoopKit to adopt the component pattern*: it cannot
pass its architecture stage without this boundary.

## Step 2 — Initialize constitutions (Act)

If constitutions do not yet exist, create them:

```
/speckit.architecture-guard.init
```

Encode the boundaries, ownership rules, and contracts you want to hold for years.
Apply the "lazy senior developer" / YAGNI stance: prefer the fewest boundaries that
keep the system honest. Do not invent structure the bridging code gives no evidence
for.

## Step 3 — Record ARC decisions (Memory)

For each significant architecture decision, create a numbered ARC document:

1. Read the `ARC` "Last used" value in
   [`docs/numbering-registry.md`](../../docs/numbering-registry.md); the new number is
   that + 1, zero-padded. **Increment the registry first.**
2. Create `docs/architecture/ARC<NNN>_<slug>.md` from
   [`templates/ARC-template.md`](templates/ARC-template.md): the decision, the context
   from the bridging code that motivated it, the rule it establishes, and how
   architecture-guard will enforce it.

## Step 4 — Review and verify

Run the architecture review over the current state:

```
/speckit.architecture-guard.architecture-workflow
```

Any violations become refactor tasks (they are handled in Stage 3, refactor-to-final).
The Goal here is not zero violations in the bridging code — it is a *clear, governed
target* the refactor stage will drive toward.

## Step 5 — Report

```
Architecture governed. Decisions recorded:
- docs/architecture/ARC<NNN>_<slug>.md ...
Constitutions: <architecture-guard location>
Open violations to resolve in Stage 3: <count>
Next: refactor bridging code to final form via /speckit.architecture-guard.governed-spec
      (Stage 3), then /speckit.engloopkit.model (Stage 4).
```

## Done when

- [ ] Brownfield mapping run
- [ ] Component boundary established (language-appropriate `components/` folder + a governed
      rule); non-vertical code still in the vertical filed as refactor tasks
- [ ] Constitutions created/refined
- [ ] Each significant decision recorded as an `ARCxxx` (registry incremented)
- [ ] `architecture-workflow` run; violations captured as refactor tasks
- [ ] Next step (governed refactor) suggested
