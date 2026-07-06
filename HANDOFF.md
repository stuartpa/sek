# HANDOFF: EngLoopKit is now available — install & practise the fix→release→upgrade loop

**Audience:** the chat/fork developing **SEK**.
**Date:** 2026-07-06.
**TL;DR:** A new Spec Kit bundle, **EngLoopKit**, now exists and is released at **v1.2.0**.
SEK should consume it as a proper downstream user. This document says what it is, how it is
installed (and the role of `uv`), and — most importantly — **how to get SEK through the
EngLoopKit stages without it turning into a slog: pilot on one small component first, use
that to shake out EngLoopKit's rough edges via the fix→release→upgrade loop, then scale to
the rest of SEK.**

---

## 1. What EngLoopKit is

EngLoopKit is a **Spec Kit bundle** that encodes a repeatable, token-efficient engineering
loop (seed → bridge → architect → refactor → model → explore/coverage → operate → evolve).
It provides nine `/speckit.engloopkit.*` commands and composes two companion extensions
(architecture-guard, tinyspec).

- **Repo:** https://github.com/stuartpa/engloopkit
- **Current release:** `v1.2.0`
- **Commands:** `seed`, `architect`, `model`, `explore`, `coverage`, `incident`,
  `postmortem`, `repair`, `refactor-scan`.

SEK is EngLoopKit's first real consumer. SEK has already been *filed* under EngLoopKit's
conventions: its engineering-loop artifacts live in `SEK/engloop/` (see
[`engloop/README.md`](engloop/README.md) and [`engloop/standards.md`](engloop/standards.md)).
Installing the extension below gives you the commands that produce and manage those artifacts.

---

## 2. How EngLoopKit is installed (and where `uv` fits)

**`uv` installs the `specify` CLI, not EngLoopKit.** The chain is:

1. **`uv`** installs the Spec Kit CLI (`specify`). This is the only piece `uv` touches.
2. **`specify`** then installs EngLoopKit — as a Spec Kit **extension/bundle**, from a
   **GitHub release archive (.zip)**. EngLoopKit is *not* a Python package and is *not*
   `uv`-installable directly.

So: `uv → specify → (specify extension add / specify bundle install) → EngLoopKit`.

### Prerequisite: the `specify` CLI (>= 0.12.0, via uv)

```powershell
# Install or upgrade the Spec Kit CLI (this is the part that uses uv)
uv tool install specify-cli --force --from git+https://github.com/github/spec-kit.git@v0.12.4
specify --help    # should list: bundle, extension, ...
```

> The bundle requires `speckit_version >= 0.12.0`. The `bundle` and `extension` commands
> only exist in recent Spec Kit; older `specify` (e.g. 0.0.x) must be upgraded first.

---

## 3. Install EngLoopKit into SEK (as a consumer)

Run these from the SEK repo root (`C:\boards\brd009\SEK`). SEK is already Spec Kit–
initialized (`.specify/` exists), so no `specify init` is needed.

### Option A — just the EngLoopKit commands (simplest)

```powershell
specify extension add engloopkit --force `
  --from https://github.com/stuartpa/engloopkit/releases/download/v1.2.0/engloopkit-extension-1.2.0.zip
```

Answer **`y`** to the "Untrusted Source" prompt (it is an external URL). This installs the
nine `/speckit.engloopkit.*` commands into SEK's agent command directories
(`.github/prompts/`, `.github/agents/`) and the source under `.specify/extensions/engloopkit/`.

### Option B — the full bundle (adds the companion extensions)

The `architect` stage uses **architecture-guard** and the `repair` stage uses **tinyspec**.
For the whole loop, install all three:

```powershell
specify extension add architecture-guard --force `
  --from https://github.com/DyanGalih/spec-kit-architecture-guard/archive/refs/tags/v1.11.0.zip
specify extension add tinyspec --force `
  --from https://github.com/Quratulain-bilal/spec-kit-tinyspec/archive/refs/tags/v1.0.0.zip
specify extension add engloopkit --force `
  --from https://github.com/stuartpa/engloopkit/releases/download/v1.2.0/engloopkit-extension-1.2.0.zip
```

### Verify

```powershell
specify extension list         # engloopkit (+ architecture-guard, tinyspec) present
```

The core operations commands (`seed`, `model`, `explore`, `coverage`, `incident`,
`postmortem`, `refactor-scan`) work with just Option A; `architect` and `repair` want the
companions from Option B.

### SEK consumer notes

- **Artifact root is `engloop/`** for SEK (an override of EngLoopKit's default `docs/`,
  because SEK's `docs/` is a published DocFX site). This is recorded in
  [`engloop/standards.md`](engloop/standards.md); the commands read that override. When a
  command says "`docs/…`", write it under `engloop/…` in SEK.
- Numbering counters for SEK live in [`engloop/numbering-registry.md`](engloop/numbering-registry.md)
  (increment before creating a doc).
- **The component pattern applies to SEK too.** EngLoopKit's architecture stage
  (`/speckit.engloopkit.architect`) will *enforce* it: non-vertical code (generic building
  blocks that wrap the .NET runtime/BCL and would be useful in an unrelated repo) must move
  into a top-level **`components/`** folder as class-library projects
  (`SpecExplorerKit.Components.<Name>`), and the SEK vertical composes them. Refactor cycles
  then converge toward it. This is not optional — SEK cannot pass its architecture stage
  without the boundary. (SEK already has candidates: e.g. graph algorithms, the CORD lexer
  primitives, and Z3-glue helpers are generic components; the `sek` CLI + Cord semantics are
  the vertical.) See EngLoopKit's `docs/component-pattern.md`.
- SEK does **not** vendor EngLoopKit; it consumes the released extension. Keep the two
  repos' artifacts separate.

---

## 4. How to get SEK through the stages: pilot first, then scale

The recursive run — *SEK engineering SEK, driven by EngLoopKit, which was itself built with
SEK* — is genuinely new, and nobody has done it before. So **do not take all of SEK through
the model/explore/coverage stages in one shot.** Start with a **pilot**: one small, clean
piece of SEK, taken all the way through, whose job is to flush out EngLoopKit's rough edges
cheaply before you scale.

### 4.1 Why pilot first

Stages 0–3 (seed, bridge, architect, refactor-to-final) are low-risk. The real unknowns are
Stages 4–5 — build a SEK model of real SEK code, explore it, `sek generate` conformance
tests, drive coverage — a surface **no in-repo SEK sample has exercised with a `binding`**.
Expect sharp edges there (see the `using-sek-to-generate-tests` skill): model state must be a
**property** not a field or exploration collapses; `sek generate`'s harness makes a fresh SUT
instance per call and only coerces string/enum/primitive args, so it can't directly drive
stateful or object-argument code (fall back to `sek test` or hand-written tests); unbounded
models hang. On a **small component** each of these is a five-minute incident, not a lost day.

### 4.2 The pilot — one component, all the way through

1. **Pick one small, clearly-generic component.** Use the component-pattern litmus test
   (*useful, unchanged, in a repo solving a different problem?*). Good SEK candidates: a
   graph-analysis helper, a CORD lexer primitive, or a small Z3-glue utility. **Avoid the
   big vertical** (the `sek` CLI, Cord semantics) for the pilot.
2. **Architect it (Stage 2)** — `/speckit.engloopkit.architect`: extract just that piece into
   `components/SpecExplorerKit.Components.<Name>`; the vertical composes it; record the `ARC`.
   (This also proves the component pattern on a tiny surface.)
3. **Model it (Stage 4)** — `/speckit.engloopkit.model`: a small SEK model of that component.
4. **Explore + cover (Stage 5)** — `/speckit.engloopkit.explore` then
   `/speckit.engloopkit.coverage`: get the generated (or, where `generate` can't bind,
   hand-written) tests green and that component's coverage up.
5. **Every time a command or the tool misbehaves, that is an EngLoopKit incident** — go to 4.3.

The pilot is *done* when that one component is filed, modelled, tested, and green — **and** the
commands + the `using-sek-to-generate-tests` skill have been hardened by the incidents it
raised.

### 4.3 When the pilot hits an EngLoopKit bug — the fix→release→upgrade loop

This is the engine that makes the pilot pay off. Treat EngLoopKit as an external dependency
with its own release cycle.

```
 ┌────────────────────────────────────────────────────────────────────────┐
 │ 1. USE EngLoopKit in SEK                                                │
 │    Run /speckit.engloopkit.* commands to drive SEK's engineering loop.  │
 └───────────────────────────────┬────────────────────────────────────────┘
                                 │ you hit an EngLoopKit bug
                                 ▼
 ┌────────────────────────────────────────────────────────────────────────┐
 │ 2. REPORT it back to EngLoopKit                                         │
 │    File a GitHub issue on stuartpa/engloopkit (title + repro + which    │
 │    command + expected vs actual). Do NOT fix EngLoopKit from inside SEK │
 │    — SEK only consumes it.                                              │
 └───────────────────────────────┬────────────────────────────────────────┘
                                 │ hand off to the EngLoopKit chat/fork
                                 ▼
 ┌────────────────────────────────────────────────────────────────────────┐
 │ 3. FIX in the EngLoopKit repo (its own operations loop)                │
 │    /speckit.engloopkit.incident → mitigate; then postmortem → repair.  │
 │    Land the fix; 42-test suite + `specify bundle validate` stay green. │
 └───────────────────────────────┬────────────────────────────────────────┘
                                 │
                                 ▼
 ┌────────────────────────────────────────────────────────────────────────┐
 │ 4. RELEASE a new EngLoopKit version (e.g. v1.2.0)                       │
 │    Bump extension.yml + bundle.yml + CHANGELOG; rebuild the extension   │
 │    asset; update catalog.json (download_url + sha256); tag; gh release  │
 │    with engloopkit-<v>.zip + engloopkit-extension-<v>.zip.             │
 └───────────────────────────────┬────────────────────────────────────────┘
                                 │ new release URL
                                 ▼
 ┌────────────────────────────────────────────────────────────────────────┐
 │ 5. UPGRADE EngLoopKit in SEK                                            │
 │    specify extension add engloopkit --force \                          │
 │      --from https://github.com/stuartpa/engloopkit/releases/download/   │
 │             vX.Y.Z/engloopkit-extension-X.Y.Z.zip                       │
 │    Re-verify with `specify extension list`; the fix is now live in SEK.│
 └────────────────────────────────────────────────────────────────────────┘
```

### Upgrade command (step 5), concretely

```powershell
# Replace X.Y.Z with the new release
specify extension add engloopkit --force `
  --from https://github.com/stuartpa/engloopkit/releases/download/vX.Y.Z/engloopkit-extension-X.Y.Z.zip
```

`--force` overwrites the installed copy. (`specify extension update` also exists, but it
resolves from catalogs; since EngLoopKit is installed from a custom URL, the pinned
`--from` upgrade above is the reliable path.)

### 4.4 Scale to the rest of SEK

Once the pilot is green **and** the loop has run a couple of times (so the commands and the
skill are battle-tested), repeat Stages 2–5 across SEK's other components and then its
vertical — now with the sharp edges already filed off. That is when SEK reaches
“ready for incidents / post-mortems” on its own footing, having proven the whole recursion on
a small surface first.

---

## 5. An even smaller warm-up before the pilot (optional)

EngLoopKit already has one known defect that is a perfect *first* trip through the
fix→release→upgrade loop — smaller even than the pilot, and it needs no SEK modelling:

> **`specify bundle build` zips the entire EngLoopKit repo** (including `src/`, `tests/`,
> and build outputs), bloating the bundle artifact. Installs are unaffected (the shippable
> extension asset is staged from `extensions/engloopkit/` only), but the bundle `.zip` is
> not clean.

Report it (step 2), let the EngLoopKit fork run incident → postmortem → repair (step 3),
release the next version, e.g. v1.2.1 (step 4), and upgrade SEK (step 5). It exercises the
whole loop end-to-end so the pilot in §4 starts with the release machinery already proven.

---

## 6. Boundaries (important)

- **SEK consumes; it does not modify EngLoopKit.** All EngLoopKit fixes happen in
  `stuartpa/engloopkit`, not in SEK.
- **SEK is also EngLoopKit's SUT platform.** EngLoopKit's own tests project-reference
  `SEK/src/Sek.Modeling`, so keep SEK checked out as a sibling of EngLoopKit
  (`C:\boards\brd009\SEK` next to `C:\boards\brd009\EngLoopKit`). Nothing to do in SEK for
  this; just don't move the repo.
- **Releases are pinned by URL.** SEK always upgrades to an explicit release tag, so you
  control exactly when a fix lands.
