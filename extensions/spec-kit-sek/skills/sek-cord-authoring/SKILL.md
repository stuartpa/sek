---
name: sek-cord-authoring
description: "Author, review, debug, and optimize SpecExplorerKit (SEK) Cord configurations and machines. Use when creating or changing .cord files, selecting behavior operators, constraining parameters, slicing a model, using bind/let/constructs/model checking, diagnosing empty or bounded explorations, or generating Cord-backed conformance tests."
user-invocable: false
---

# Author Cord for SEK

Use the complete **SEK-supported** Cord surface intentionally. This skill describes the current
SEK implementation, not every construct accepted by legacy Microsoft Spec Explorer. Parsing is
not proof of runtime semantics.

## Load progressively

Always read [support and safety](./references/support-and-safety.md). Then load only what is needed:

- [Implemented language](./references/implemented-language.md) — declarations, constraints,
  precedence, operators, constructs, events, and return bindings.
- [Operator semantics](./references/operator-semantics.md) — accepted traces, acceptance,
  precedence examples, alphabet rules, products, failure scope, and review checklist.
- [Authoring patterns](./references/authoring-patterns.md) — safe recipes for models, slices,
  parameter generation, behavior mode, model checking, steering, and test machines.

Copy from the parse-validated assets when starting a new script:

- [Model and scenario slice](./assets/model-and-slice.cord)
- [Root-level behavior algebra](./assets/behavior-mode.cord)
- [Advanced constructs](./assets/advanced-constructs.cord)

Statuses are binding:

- **Supported** — parsed and consumed with tested runtime semantics.
- **Conditional** — usable only with the stated restrictions and verification.
- **Parsed-only** — accepted syntax without useful runtime semantics; do not author it.
- **Unsupported** — do not author it.

## Procedure

1. Inventory model `[Rule("Label")]` methods, parameter types, `[Domain]` methods, guards,
   accepting conditions, and expected rejection outcomes.
2. Keep a direct unsliced model-program machine. Add slices only for a required trace language;
   use behavior mode only for a pure protocol with no C# model state.
3. Prefer explicit actions. Use `action all` only after proving it resolves exactly the intended
   rules; current no-match behavior can expose all rules.
4. Give every scalar parameter a finite domain using `Condition.In`, model `[Domain]`, natural
   enum/Boolean domains, or a bounded `let`/`bind`.
5. Select the strongest applicable supported feature: argument pinning, combinations, top-level
   slicing/parallel, `bind`, `let`, accepting/test constructs, steering, or model checking.
6. Keep parallel products at the current/root composition, `bind` top-level, and constructs around
   model-backed targets.
7. Build and run `sek validate`; treat warnings, ignored-looking clauses, unknown names, and
   ambiguous action sets as failures.
8. Explore each proof machine and verify states, transitions, acceptance, actions, arguments,
   bounds, goals/requirements, fail states, and negative transitions against explicit expectations.
9. Regenerate and replay tests from current model and binding binaries. Reject stale artifacts.
10. If required semantics are Conditional, Parsed-only, Unsupported, or version-ambiguous, stop or
    use an explicit custom harness; never silently weaken the proof.

## Feature chooser

| Need | Preferred shape |
|---|---|
| Complete reachable model | `construct model program from Config` |
| Focused legal traces | `Scenario || ModelProgram` at the top level |
| Model-derived rejection tests | Generate from the unsliced model machine |
| Pin action arguments | `Action(value, _)` |
| Finite values and predicates | `Condition.In` + `Condition.IsTrue` |
| Pair coverage | `Combination.Pairwise` |
| Reproducible rare-domain ordering | `Probability.IsTrue` + `RandomSeed` |
| Machine-local domain override | Top-level `bind ... in ModelProgram` |
| Shared local scenario values | Bounded `let ... in Behavior` |
| Pure protocol | Direct behavior machine, no model construct |
| Acceptance pruning | `construct accepting paths for ModelBackedMachine` |
| Generated path strategy | `construct test cases where strategy="shorttests"|"longtests" ...` |
| Forbidden trace | `AntiScenario : fail` plus an external fail-state verdict |
| Requirement observation | Model `Requirement.Capture` + `construct requirement coverage` |

## Proof rules

- Unsliced model exploration and sliced scenarios prove different properties. Current sliced
  exploration does not emit model-derived negative transitions.
- A successful process exit, parse, or non-empty output file is not sufficient. Reject bound hits,
  suspicious/empty graphs, missing actions, unexpected fail states, unmet requirements, or missing
  rejection tests.
- Generated `Observe` is currently a direct call, and generated replay does not dynamically thread
  a SUT return value. Use a custom harness when those semantics are required.
- Fail closed on missing or ambiguous model identity, action universe, domain, machine reference,
  construct semantics, installed SEK version, or graph outcome.
