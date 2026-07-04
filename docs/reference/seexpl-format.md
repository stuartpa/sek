---
title: .seexpl format
description: The JSON structure of a SpecExplorerKit transition-system document.
---

# `.seexpl` format

`.seexpl` is the JSON document SEK writes for an exploration. It captures a
transition system: states, labeled transitions, and summary metadata. It is an open,
diff-friendly format you can view with [`sek view`](cli.md#sek-view) or post-process
with any JSON tooling.

## Shape

```jsonc
{
  "machine": "AccountExploration",
  "initialStateId": "S0",
  "states": [
    { "id": "S0", "hash": "…", "initial": true,  "accepting": true,  "label": null },
    { "id": "S1", "hash": "…", "initial": false, "accepting": true,  "label": null }
    // …
  ],
  "transitions": [
    {
      "from": "S0",
      "action": { "label": "AccountImpl.CreateAccount", "args": [] },
      "to": "S1"
    },
    {
      "from": "S1",
      "action": { "label": "AccountImpl.SetBalance", "args": ["{\"Balance\":0}", "10"] },
      "to": "S2"
    }
    // …
  ],
  "metadata": {
    "states": "10",
    "transitions": "58",
    "accepting": "10",
    "hitBound": "False"
  }
}
```

## Fields

### States

| Field | Meaning |
|---|---|
| `id` | stable identifier (`S0`, `S1`, …); `S0` is the initial state |
| `hash` | canonical hash of the serialized state (state identity) |
| `initial` | `true` for the initial state |
| `accepting` | `true` if all accepting conditions held in this state |
| `label` | optional display label |

### Transitions

| Field | Meaning |
|---|---|
| `from` | source state id |
| `action.label` | the action's label (matches a model rule / Cord action) |
| `action.args` | stringified argument values (objects are JSON-encoded) |
| `to` | target state id |

### Metadata

Counts of `states`, `transitions`, and `accepting` states, plus `hitBound`
(whether a bound stopped the search).

## Determinism

The document is produced deterministically, so it is safe to commit and diff. A
change in the `.seexpl` corresponds to a real change in modeled behavior.

## Rendering

```bash
sek view graph.seexpl --format mermaid   # stateDiagram-v2
sek view graph.seexpl --format dot       # Graphviz
sek view graph.seexpl --format html      # self-contained page
```

## Related

- [Transition systems](../concepts/transition-systems.md)
- [CLI: `sek view`](cli.md#sek-view)
