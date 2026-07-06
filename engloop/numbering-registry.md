# Numbering Registry (SEK)

Single source of truth for SEK's EngLoopKit document counters. **Increment the "Last used" value
here before creating a new document.** See [standards.md](standards.md) for rules and prefix
definitions. Artifact root is `engloop/` (see standards.md).

## Global counters

| Prefix | Scope | Last used | Notes |
|---|---|---|---|
| `SEED` | Gathering docs | `SEED001` | SEED001 = Spec Explorer → SEK port |
| `BRG` | Bridging-stage records | `BRG002` | BRG001 = Cord implementation state; BRG002 = parity & sample audit |
| `SP` | Specs | `SP000` | none yet (bridging code predates a recorded specify loop) |
| `ARC` | Architecture decisions | `ARC002` | ARC001 = Cord front-end as a compiler; ARC002 = component boundary (components/ vs vertical) |
| `MDL` | SEK models | `MDL000` | none yet |
| `CRD` | CORD explorations | `CRD000` | none yet |
| `COV` | Coverage reports | `COV000` | none yet |
| `IN` | Incidents | `IN000` | none yet |
| `PM` | Post-mortems | `PM000` | none yet |
| `REF` | Refactor decisions | `REF000` | none yet |

## Local counters

Reset inside each parent; tracked in the parent doc, not here.

| Prefix | Resets per | Recorded in |
|---|---|---|
| `MIT` | Incident | the incident's timeline table |
| `LRN` | Post-mortem | the post-mortem's Learnings section |
| `RPI` | Post-mortem | the post-mortem's Repair Items section |
