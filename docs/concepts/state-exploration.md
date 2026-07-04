---
title: State exploration
description: How SpecExplorerKit performs deterministic breadth-first exploration of a model into a transition system.
---

# State exploration

*Exploration* is the process that turns a model program plus a Cord scenario into a
**transition system**: a graph whose nodes are states and whose edges are labeled
action invocations.

## The algorithm

SEK performs a deterministic **breadth-first search** from the initial state:

1. **Initial state.** Instantiate the model, snapshot it (serialize to JSON),
   compute its canonical hash, and mark it as state `S0`. Evaluate accepting
   conditions.
2. **Frontier.** Dequeue a state. If it is at the depth bound, skip expansion.
3. **For each rule**, resolve its parameter domains for the current state, then
   generate argument combinations (via [Z3 or enumeration](parameter-generation.md)).
4. **For each argument combination**, invoke the rule on a *fresh copy* of the
   state. If a guard disables it, skip. Otherwise snapshot the resulting state.
5. **De-duplicate** the successor by canonical hash. If new, assign an id and
   enqueue it; either way, record a transition `from --action(args)--> to`.
6. **Stop** when the frontier is empty or a bound is hit.

Because rules are considered in a stable order, domains are enumerated in order, and
hashing is canonical, the resulting graph is **deterministic** — identical inputs
always yield an identical `.seexpl`.

## Bounds

Real models can have infinite state spaces (e.g., an unbounded message queue).
Cord switches bound the search:

| Switch | Effect |
|---|---|
| `StateBound` | stop after this many distinct states |
| `StepBound` | stop after this many transitions |
| `PathDepthBound` | do not expand beyond this depth from `S0` |

When a bound stops the search, `sek explore` reports **`(bound hit)`**. That's a
signal to either tighten the scenario or accept a truncated view — see the
[chat](../samples/chat.md) and [PubSub](../samples/pubsub.md) samples, whose queues
are intentionally unbounded.

## State identity and de-duplication

Two states are "the same" when their canonical JSON hashes match. Canonicalization
normalizes structure (for example, order-insensitive collections) so that
data-equal states collapse to one node. This keeps graphs finite and meaningful for
set-like and object-graph state. See
[Model programs → State and identity](model-programs.md#state-and-identity).

## Behavior mode

When a machine composes **behavior** with Cord operators over abstract actions
(rather than `construct model program from ...`), SEK explores the *behavior
automaton* instead: it compiles the Cord expression into an automaton (Thompson-style
construction with parallel product, permutation, and loose-sequence handling) and
enumerates its runs. This is how the [Operators sample](../samples/operators.md)
explores `;`, `|`, `||`, `|||`, `|?|`, `->`, `&`, `*`, `+`, `?`, `{n}`, `_`, `...`,
and `!` without any model state.

## Output

Exploration writes a [`.seexpl` transition system](transition-systems.md) and prints
a summary: states, transitions, accepting states, and whether a bound was hit.

## Related

- [Transition systems (.seexpl)](transition-systems.md)
- [Parameter generation](parameter-generation.md)
- [The Cord language](cord-language.md)
