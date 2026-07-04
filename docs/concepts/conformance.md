---
title: Conformance
description: How SpecExplorerKit replays an exploration against a real implementation to check conformance.
---

# Conformance

*Conformance* answers the question: **does the real implementation behave the way
the model says it should?** SEK checks this by replaying an explored transition
system against a binding to the system under test (SUT).

## The idea

Exploration produces a graph of states and labeled transitions that the *model*
permits. Conformance walks that graph and, for each transition, calls the
corresponding operation on the real system. If every modeled transition is
reproducible by the implementation, the implementation **conforms**.

## Bindings

A **binding** maps model action labels to implementation calls. It is an adapter
assembly plus a namespace; `sek test` reflects over that namespace and, for each
action, finds a method whose name matches the action (the part after the last `.`)
and invokes it with the transition's arguments.

```json
"binding": { "assembly": "Adapter/bin/Debug/Adapter.dll", "namespace": "Adapter" }
```

Keep bindings **thin**: translate an action + arguments into a single call on the
real system, and optionally check the return value.

## Running it

```bash
sek test <machine> --project path/to/project
```

`sek test` explores the machine, then replays every transition and reports:

```text
  transitions replayed : 12706
  succeeded            : 12706
  failed               : 0
  actions covered      : 10 (…)
TEST PASSED
```

## Interpreting failures

A failure means the implementation diverged from the model on some transition. This
is either:

- an **implementation bug** — the code doesn't do what the spec says; fix the code;
  or
- a **model/spec drift** — intended behavior changed and the model is stale; update
  the model.

The report lists the first failing transitions to localize the divergence.

## The flagship example

The **TPC-C** model explores to 2,446 states and 12,706 transitions and replays all
12,706 against a fake implementation with **zero failures**, covering all ten
transaction actions — an end-to-end demonstration of explore → verify.

## Where it fits

Conformance is the verification end of the loop. In a spec-driven workflow it acts
as a gate: full transition coverage with zero failures is strong evidence the
implementation conforms to the specified behavior. See
[Using SEK as a Spec Kit extension](../community/spec-kit-extension.md) and the
guide [Running conformance](../guides/conformance.md).

## Related

- Guide: [Running conformance](../guides/conformance.md)
- [Transition systems (.seexpl)](transition-systems.md)
- [State exploration](state-exploration.md)
