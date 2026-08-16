---
name: sek-cord-authoring
description: "Author, review, debug, and optimize SpecExplorerKit (SEK) Cord files. Use for .cord syntax, configs, actions, finite domains, combinations, behavior operators, model slicing, bind/let, constructs, model checking, empty graphs, bounds, and Cord-backed generated tests."
user-invocable: false
---

# Load SEK's Cord authoring authority

This wrapper makes the skill discoverable while keeping the released Spec Kit extension as the
single self-contained agent resource. Read:

[`extensions/spec-kit-sek/skills/sek-cord-authoring/SKILL.md`](../../../extensions/spec-kit-sek/skills/sek-cord-authoring/SKILL.md)

Always load its support matrix before changing Cord, then load the language and pattern references
needed for the task. If a required file is missing, stop; do not infer behavior from legacy Spec
Explorer syntax or from parser acceptance alone.
