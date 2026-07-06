# engloopkit extension

The command set for [EngLoopKit](../../README.md) — the stages of the engineering loop
that core Spec Kit doesn't provide. Install this on its own, or (recommended) via the
`engloopkit` bundle which also pulls in `architecture-guard` and `tinyspec`.

Every command is written as a Loop Engineering loop with an explicit **Trigger · Goal ·
Actions · Verification · Memory**, and every command produces a numbered document per
the [document standards](../../docs/standards.md).

## Commands

| Command | Stage | Produces |
|---|---|---|
| `/speckit.engloopkit.seed` | 0 · Seed | `SEEDxxx` |
| `/speckit.engloopkit.architect` | 2 · Architect | `ARCxxx` (+ architecture-guard constitutions) |
| `/speckit.engloopkit.model` | 4 · Model | `MDLxxx` |
| `/speckit.engloopkit.explore` | 5 · Explore | `CRDxxx` + generated tests |
| `/speckit.engloopkit.coverage` | 5 · Coverage | `COVxxx` |
| `/speckit.engloopkit.incident` | 6 · Incident | `INxxx` (+ `MIT`) |
| `/speckit.engloopkit.postmortem` | 6 · Post-mortem | `PMxxx` (+ `LRN`, `RPI`) |
| `/speckit.engloopkit.repair` | 6 · Repair | routes to `tinyspec` / `specify` |
| `/speckit.engloopkit.refactor-scan` | 7 · Evolve | `REFxxx` → `SEEDxxx` |

Stages 1 (bridge) and 3 (refactor to final) use core Spec Kit commands directly; they
have no engloopkit command of their own by design — see
[the engineering loop](../../docs/engineering-loop.md).

## Install (dev)

```bash
specify extension add --dev ./EngLoopKit/extensions/engloopkit
```

## Templates

Artifact templates live in [`templates/`](templates/) and are referenced by the
commands. They exist so every produced document is consistent and greppable.

## License

MIT.
