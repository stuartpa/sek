---
title: Transition systems (.seexpl)
description: The .seexpl transition-system format that SpecExplorerKit emits, and how to view and post-process it.
---

# Transition systems (`.seexpl`)

The result of exploration is a **transition system** — a directed graph whose nodes
are states and whose edges are labeled action invocations. SEK serializes it as an
open JSON document with the `.seexpl` extension.

## What's in it

A `.seexpl` document records:

- **Machine** — the name of the explored machine.
- **States** — each with an id (`S0`, `S1`, …), a canonical state hash, whether it is
  the **initial** state, and whether it is **accepting**.
- **Transitions** — each with a *from* state, an **action invocation** (label plus
  stringified arguments), and a *to* state.
- **Metadata** — counts of states/transitions/accepting states and whether a bound
  was hit.

Because it's plain JSON, `.seexpl` is diff-friendly, reviewable in pull requests,
and easy to post-process with any tool.

## Viewing

`sek view` renders a `.seexpl` into a browsable or embeddable form:

```bash
sek view path/to/graph.seexpl --format mermaid          # to stdout (great for PRs/docs)
sek view path/to/graph.seexpl --format dot  --out g.dot # Graphviz
sek view path/to/graph.seexpl --format html --out g.html
```

- **mermaid** — a `stateDiagram-v2` you can paste into Markdown; initial and
  accepting states are marked.
- **dot** — Graphviz input for high-quality layouts.
- **html** — a self-contained page for quick browsing.

In VS Code, the bundled **view-seexpl** skill renders graphs inline.

## Post-processing

Since `.seexpl` is JSON, you can build reports directly from it — coverage
summaries, requirement traceability, or custom visualizations. The
[RequirementReport sample](../samples/requirement-report.md) shows the modern
analog of Spec Explorer's post-processing: generate HTML reports from explored
graphs with `sek view`.

## Determinism

Exploration is deterministic (stable rule order, ordered domains, canonical
hashing), so the same inputs always produce the same `.seexpl`. That makes graphs
safe to commit and compare over time — a change in the graph reflects a real change
in modeled behavior.

## Related

- [State exploration](state-exploration.md)
- [CLI reference: `sek view`](../reference/cli.md#sek-view)
- Reference: [`.seexpl` format](../reference/seexpl-format.md)
