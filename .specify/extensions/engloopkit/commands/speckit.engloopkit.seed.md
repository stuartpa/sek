---
description: Stage 0 — Gather everything known about a thing to build into one numbered SEED document that a specify loop can start from with no other context.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). It names the
thing to build (or points at a `REF` refactor decision).

## Artifact root

Where a path below is written `docs/…`, read it as `<ARTIFACT_ROOT>/…` — the project's
artifact root (**default `docs/`**; a project whose `docs/` is a published site overrides
it, e.g. `engloop/`, in its local `standards.md`). The numbering registry is the
project's `<ARTIFACT_ROOT>/numbering-registry.md`, not the bundle's copy.

## Loop definition

- **Trigger:** a new thing to build, or a `REFxxx` from the monthly refactor scan.
- **Goal:** exactly one `docs/seeds/SEEDxxx_<slug>.md` that contains everything a
  `/speckit.specify` run needs — with no other context required.
- **Actions:** search the repo, read linked material, interview the user, collect
  snippets and constraints.
- **Verification:** the SEED answers *what*, *why*, *for whom*, and *within what
  constraints* without hand-waving; a reader could start `specify` from it alone.
- **Memory:** `docs/seeds/SEEDxxx_<slug>.md`.

## Step 0 — Assign the SEED number

1. Open [`docs/numbering-registry.md`](../../docs/numbering-registry.md). Read the
   `SEED` "Last used" value.
2. The new number is that value + 1, zero-padded to three digits.
3. **Update the registry** `SEED` row to the new number before creating the file
   (increment-first rule).
4. Derive a 2–4 word `<slug>` from the ask (kebab-case).
5. The file path is `docs/seeds/SEED<NNN>_<slug>.md`.

## Step 1 — Gather (Observe)

Cast a wide net. Do not summarize prematurely — a SEED is raw material, not a spec.

1. **The ask.** Restate what the user wants in their words. If it came from a `REF`,
   read that refactor decision and carry its rationale in.
2. **Prior art in the repo.** Search for existing code, docs, specs (`SPxxx`), models
   (`MDLxxx`), and related incidents/post-mortems that touch this area. Link them.
3. **External material.** Any URLs, standards, or references the user supplies — read
   them and extract the relevant parts inline (don't just link).
4. **Constraints.** Performance, compatibility, security, deadlines, existing
   architecture (`ARCxxx`) the thing must honor.
5. **Snippets.** Paste representative code/config/schema that the implementation will
   interact with.
6. **Open questions.** Anything ambiguous — record it; ask the user the few questions
   that would most change the design.

Use the file-search, grep, and read tools freely here; this is the cheap, deterministic
part of the loop. Ask the user only the questions a search can't answer.

## Step 2 — Compose the SEED (Act)

Create `docs/seeds/SEED<NNN>_<slug>.md` from
[`templates/SEED-template.md`](templates/SEED-template.md). Fill every section. Keep
raw material raw; mark your own inferences clearly as inferences.

## Step 3 — Verify

Check the Goal: could someone run `/speckit.specify` from this SEED alone? If a section
is thin, go back to Step 1 for that section. Do not pad — completeness, not length.

## Step 4 — Report

Tell the user the SEED path and number, list any open questions that still need their
input, and suggest the next step:

```
SEED<NNN> created: docs/seeds/SEED<NNN>_<slug>.md
Next: /speckit.specify (Stage 1 — bridging code) using this SEED as the source.
```

## Done when

- [ ] `SEED` counter incremented in the registry
- [ ] `docs/seeds/SEED<NNN>_<slug>.md` created and complete
- [ ] Open questions surfaced to the user
- [ ] Next step (specify) suggested
