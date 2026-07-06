# SEK Document Standards (EngLoopKit)

SEK follows [EngLoopKit](https://github.com/stuartpa/engloopkit)'s document standards. This file
is the SEK-local copy, with one project-specific override recorded below. The authoritative
methodology and prefix definitions are EngLoopKit's `docs/standards.md`; this file states how they
apply here.

## Artifact-root override

- **EngLoopKit default artifact root:** `docs/`
- **SEK artifact root:** **`engloop/`**

SEK's `docs/` is a *published DocFX site* whose config globs `**/*.md`. Placing process artifacts
there would publish them as product documentation, so SEK overrides the artifact root to
`engloop/`. Wherever EngLoopKit's standards say `docs/<kind>/`, read `engloop/<kind>/` for SEK.

## Naming

Every artifact is `<PREFIX><NNN>_<short-title>.md` — a fixed prefix, a monotonically increasing,
never-reused, zero-padded number, and a brief title. Increment the counter in
[numbering-registry.md](numbering-registry.md) **before** creating the file.

## Prefixes in use

| Prefix | Scope | Location | Stage |
|---|---|---|---|
| `SEED` | Gathering docs | `engloop/seeds/` | 0 |
| `BRG` | Bridging-stage records | `engloop/bridging/` | 1 |
| `SP` | Specs (`specify`) | repo-root `specs/SPxxx-*/` | 1, 3 |
| `ARC` | Architecture decisions | `engloop/architecture/` | 2 |
| `MDL` | SEK models | `engloop/models/` | 4 |
| `CRD` | CORD explorations | `engloop/cord/` | 5 |
| `COV` | Coverage reports | `engloop/coverage/` | 5 |
| `IN` | Incidents (`MIT` in-doc) | `engloop/incidents/` | 6 |
| `PM` | Post-mortems (`LRN`/`RPI` in-doc) | `engloop/postmortems/` | 6 |
| `REF` | Refactor decisions | `engloop/refactors/` | 7 |

> **`BRG`** is a SEK addition to the standard EngLoopKit prefixes: a *bridging-stage record* —
> implementation-state / parity / audit notes produced while getting the bridging code working
> (Stage 1), before the architecture stage formalizes things. It is proposed for adoption upstream
> in EngLoopKit.

## The Golden Rule

A patch applied during an incident is a **Mitigation (MIT)**, not a fix. A fix is not done until it
is committed to source, built, deployed, and has passed all verification tests.
