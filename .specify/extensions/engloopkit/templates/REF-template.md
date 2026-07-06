# REF<NNN>: <short title>

- **Date:** <date>
- **Cadence:** <e.g. month-end scan>
- **Budget:** <tokens available this cycle>
- **Status:** CHOSEN | NONE-THIS-CYCLE
- **Emitted SEED:** SEED<NNN>

## Signals gathered

| Signal | Finding |
|---|---|
| Recurring cause-classes (POSTMORTEM INDEX) | |
| Architecture drift / boundary violations | |
| Duplicated business logic (DRY) | |
| Hot spots (change frequency × complexity) | |
| Test speed vs coverage | |

## Decision-tree branch taken

<Which of the ordered branches fired (1 recurring cause-class → 2 drift → 3 DRY →
4 hot spot → 5 test speed → 6 none), and why it fired before the others.>

## Chosen refactor

<The single refactor selected. Scope it tightly.>

## Expected long-term benefit

<Why this most improves the product's multi-year health.>

## Rationale for not choosing the others

<One line each on the branches that did not fire this cycle.>

## Hand-off

- Emitted `SEED<NNN>` — proceed via `/speckit.specify` (governed) → model → explore → coverage.
