---
title: What is model-based testing?
description: An introduction to model-based testing and how SpecExplorerKit applies it.
---

# What is model-based testing?

**Model-based testing (MBT)** is a technique where you describe the *intended*
behavior of a system as a compact, executable **model**, then derive tests (and
insight) from that model automatically — instead of hand-writing every test case.

## The core idea

A model captures **what** the system should do, abstracting away **how** it does
it. From a good model you can:

- **Explore** all the reachable behaviors up to some bound, producing a
  **transition system** (a graph of states and the actions that move between them).
- **Review** that graph to find surprising or missing behavior.
- **Generate tests** that cover the graph's states, transitions, or paths.
- **Check conformance**: replay the modeled behavior against a real implementation
  and confirm the implementation agrees with the model.

Because the model is much smaller than the implementation, it's easier to get
right, easier to review, and it can expose edge cases that example-based tests miss.

## Where SEK fits

SEK is a model-based testing toolkit. In SEK:

- A **model program** is a C# class whose public properties are the state and whose
  `[Rule]` methods are the actions ([Model programs](model-programs.md)).
- **Guards** (`Require`) decide when an action is enabled ([Rules and guards](rules-and-guards.md)).
- **Cord** scripts configure exploration and compose behavior ([The Cord language](cord-language.md)).
- The **explorer** does a deterministic breadth-first search to build a
  [transition system](transition-systems.md).
- **Z3** generates action parameters from declarative constraints
  ([Parameter generation](parameter-generation.md)).
- **Conformance** replays the graph against your implementation
  ([Conformance](conformance.md)).

## When to use MBT

MBT shines when a system has interesting *stateful* or *protocol* behavior: order
of operations matters, actions have preconditions, and the interactions between
actions are where bugs hide — think schedulers, protocols, workflows, state
machines, and APIs with lifecycle rules. The classic SEK
[samples](../samples/index.md) (a chat protocol, SMB2, a task scheduler, publish/
subscribe, TPC-C transactions) are exactly these kinds of systems.

## MBT vs. example-based tests

MBT complements, rather than replaces, example-based unit tests:

| | Example-based tests | Model-based testing |
|---|---|---|
| You write | each case by hand | one model |
| Coverage | what you thought of | everything reachable within bounds |
| Edge cases | easy to miss | surfaced by exploration |
| Maintenance | many tests to update | update the model |
| Best for | pure functions, small units | stateful/protocol behavior |
