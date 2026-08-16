---
name: using-sek-to-generate-tests
description: "Consume SpecExplorerKit (SEK) from a downstream project: scaffold a model/Cord project, configure the SUT binding, run validate/explore/test/generate, and diagnose binding or generated-replay failures."
user-invocable: false
---

# Load SEK's downstream-consumer authority

Read the self-contained skill shipped by the SEK Spec Kit extension:

[`extensions/spec-kit-sek/skills/using-sek-to-generate-tests/SKILL.md`](../../../extensions/spec-kit-sek/skills/using-sek-to-generate-tests/SKILL.md)

For Cord language semantics, also load
[`sek-cord-authoring`](../sek-cord-authoring/SKILL.md). If either authority is unavailable, stop;
do not reconstruct binding or Cord behavior from a downstream product's documentation.
