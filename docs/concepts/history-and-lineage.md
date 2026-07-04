---
title: History and lineage
description: How SpecExplorerKit relates to Microsoft Spec Explorer and the Cord language.
---

# History and lineage

## Microsoft Spec Explorer

**Spec Explorer** was a model-based testing tool from Microsoft Research and the
Windows protocol documentation program. It let engineers write model programs in
C#, describe test scenarios in a dedicated language called **Cord** (COndition and
Reaction Descriptions), explore models into transition systems, visualize them in
Visual Studio, and generate/execute conformance tests. It was used heavily to test
the Windows interoperability protocols (for example, the MS-* protocol suites).

Spec Explorer's strengths were its expressive scenario language, its parameter
generation, and its tight conformance loop. Its constraints, by today's standards,
were that it was tied to **.NET Framework**, the **`Microsoft.Modeling`** runtime,
and a **Visual Studio** extension with a proprietary designer and output format.

## What SpecExplorerKit revives

SEK is a clean-room, modern revival of those ideas:

- **Cord is preserved** as a first-class scenario and configuration language.
- The **model / explore / view / verify** workflow is intact.
- The tooling is **CLI-first** (`sek`), cross-platform, and **.NET 8**-native —
  no Visual Studio and no Windows-only runtime.
- Parameter generation is powered by the **Z3** theorem prover.
- Explorations are emitted as open **`.seexpl` JSON** and rendered to Mermaid, DOT,
  or HTML — reviewable in any editor and in pull requests.

## What is intentionally different

- The modeling runtime is a small, purpose-built library (`Sek.Modeling`) rather
  than `Microsoft.Modeling`. State lives in public properties and is snapshotted as
  JSON; set-equality is handled by canonical hashing.
- Some advanced Spec Explorer scenario-control constructs (`bind`,
  `construct point shoot`, `construct bounded exploration`) are on the roadmap. The
  common `construct model program from <Config>` and the full behavior operator
  algebra are supported today.
- SEK ships as a [Spec Kit community extension](../community/spec-kit-extension.md),
  bringing model-based testing into modern spec-driven development.

## The samples as continuity

To demonstrate fidelity, the original Spec Explorer 2010 sample suite has been
ported to SEK and validated (see [Samples](../samples/index.md)). Comparing
`samples-source/` (originals) with `samples/` (SEK ports) shows the lineage
concretely.
