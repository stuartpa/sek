---
title: RequirementReport sample
description: Post-processing an exploration into a browsable report — the modern analog of Spec Explorer's requirement reporting.
---

# RequirementReport

**Demonstrates:** post-processing an exploration into a report.

- **Project:** `samples/RequirementReport`

## Background

The classic Spec Explorer `RequirementReport` sample is **not** a model to explore —
it is a *post-processing tool* (`PostProcessorSample`) that reads explored transition
systems (via `Microsoft.SpecExplorer.ObjectModel`) and emits a requirement-coverage
report.

## The SEK analog

Because SEK emits open [`.seexpl` JSON](../reference/seexpl-format.md), post-processing
is straightforward. The modern analog of RequirementReport is the
[`sek view`](../reference/cli.md#sek-view) command, which turns any explored graph
into a browsable **HTML** report (or Mermaid / DOT). The sample's `report.ps1`
regenerates HTML reports for several explored graphs:

```bash
pwsh samples/RequirementReport/report.ps1
# writes samples/RequirementReport/reports/{Account,atsvc,SMB2}.html
```

## Building your own reports

Since `.seexpl` is plain JSON, you can also write custom reports — coverage
summaries, requirement traceability matrices, dashboards — by reading the document
directly. `sek view --format html` is the built-in starting point.

## Related

- [Transition systems (.seexpl)](../concepts/transition-systems.md)
- [`.seexpl` format](../reference/seexpl-format.md)
