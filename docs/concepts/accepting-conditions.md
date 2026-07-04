---
title: Accepting conditions
description: How accepting conditions mark goal states for path construction and test generation.
---

# Accepting conditions

An **accepting condition** marks which states are *goal* (or "complete") states. A
state is **accepting** when **all** accepting conditions return `true` in that state.

```csharp
[AcceptingCondition]
public bool AllDelivered() =>
    Users.All(u => u.Inbox.Count == 0);
```

An accepting condition is a parameterless `bool` method marked
`[AcceptingCondition]`. You may declare several; the state is accepting only if
every one holds.

## Why they matter

Accepting states are the targets for constructing complete behaviors:

- **`construct accepting paths for <machine>`** keeps only paths that end in an
  accepting state.
- **`construct test cases for <machine>`** derives tests whose runs end in
  accepting states.

In model-based testing terms, accepting states usually encode "the protocol run
finished cleanly" or "the acceptance criterion is satisfied". Reaching one during
exploration is evidence that the corresponding behavior is achievable.

> [!IMPORTANT]
> If a model declares **no** accepting conditions, then **no** state is accepting.
> Give your model at least one accepting condition (even `=> true`) if you want
> every state to count as a valid stopping point.

## Examples from the samples

- **SMB2**: accepting when the session is torn down — `!SessionUp && Trees.Count == 0`
  — i.e. a full connect → work → disconnect run completed.
- **chat**: accepting when the protocol is quiescent — everyone logged on and no
  broadcasts pending acknowledgement.
- **Sailboat**: accepting at the initial position (`X == 0 && Y == 0`), modeling a
  completed there-and-back voyage.

## Relationship to guards

Guards decide *when an action can happen*; accepting conditions decide *when a state
is a valid goal*. They are independent: a state can be accepting yet still have
enabled actions leading elsewhere (e.g., you can keep going after a clean stop).

## Related

- [Model programs](model-programs.md)
- [State exploration](state-exploration.md)
- [The Cord language](cord-language.md)
