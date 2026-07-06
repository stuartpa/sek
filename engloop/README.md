# `engloop/` — SEK's EngLoopKit artifact root

This folder is SEK's **engineering-loop artifact root**. SEK is developed with
[EngLoopKit](https://github.com/stuartpa/engloopkit), and every process artifact it produces
(seeds, architecture decisions, models, explorations, coverage reports, incidents, post-mortems,
refactors) lives here — deliberately **outside** `docs/`, which is SEK's *published DocFX product
site*. Keeping the two apart means process artifacts never leak into the published documentation.

> Artifact root = `engloop/` (an override of EngLoopKit's default `docs/`; see
> [standards.md](standards.md)). Numbering is tracked in
> [numbering-registry.md](numbering-registry.md).

## Layout

```
engloop/
├── standards.md            # doc prefixes, numbering rules, artifact-root convention
├── numbering-registry.md   # the counters (increment before creating a doc)
├── seeds/                  # SEEDxxx — gathering docs (Stage 0)
├── bridging/               # BRGxxx — bridging-stage records (Stage 1)
├── architecture/           # ARCxxx — architecture decisions (Stage 2)   ← next
├── models/                 # MDLxxx — SEK models (Stage 4)
├── cord/                   # CRDxxx — CORD explorations (Stage 5)
├── coverage/               # COVxxx — coverage reports (Stage 5)
├── incidents/              # INxxx  — incidents (Stage 6, + MIT in-doc)
├── postmortems/            # PMxxx  — post-mortems (Stage 6, + LRN/RPI in-doc)
└── refactors/              # REFxxx — refactor decisions (Stage 7)
```

Spec Kit's own `specify` outputs (SPxxx) live at the repo-root `specs/` directory, per Spec Kit
convention — not under `engloop/`.

## Stage status (as of retroactive filing, 2026-07-06)

| Stage | State | Artifacts |
|---|---|---|
| 0 · Seed | ✅ complete | [SEED001](seeds/SEED001_spec-explorer-port.md) |
| 1 · Bridge (bridging code) | ✅ complete | [BRG001](bridging/BRG001_cord-implementation-state.md), [BRG002](bridging/BRG002_cord-parity-and-sample-audit.md); working engine, 49 tests, 9 samples byte-identical, CI regression gate |
| 2 · Architect | ⏭ next | — |
| 3 · Refactor to final | pending | — |
| 4 · Model | pending | — |
| 5 · Explore / Coverage | pending | — |
| 6 · Operate | pending | — |
| 7 · Evolve | pending | — |

SEK is at the end of the **bridging code** stage. The next EngLoopKit step is
`/speckit.engloopkit.architect`.
