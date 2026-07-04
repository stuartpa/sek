---
title: Model programs
description: How a SpecExplorerKit model program represents state, actions, and behavior.
---

# Model programs

A **model program** is the C# artifact at the heart of a SEK model. It defines the
*state space* and the *actions* that move between states.

## Anatomy

```csharp
using Sek.Modeling;

public sealed class Turnstile : ModelProgram   // 1. derive from ModelProgram
{
    public bool Locked { get; set; } = true;   // 2. state = public properties

    [Rule("Turnstile.Coin")]                    // 3. actions = [Rule] methods
    public void Coin()
    {
        Require(Locked, "already unlocked");    // 4. guards with Require
        Locked = false;                         // 5. effect = state mutation
    }

    [AcceptingCondition]                        // 6. goal states
    public bool AtRest() => Locked;
}
```

1. **Derive from `ModelProgram`** — the base class provides `Require`.
2. **State is public properties.** The engine snapshots a state by serializing the
   instance to JSON, so state must be public, read/write, and JSON-friendly, and the
   class must have a parameterless constructor.
3. **Actions are `[Rule]` methods.** The action label is `Type.Method` by default,
   or supply one: `[Rule("Area.Action")]`.
4. **Guards** with `Require(condition, "reason")` — see [Rules and guards](rules-and-guards.md).
5. **Effects** are ordinary state mutations.
6. **Accepting conditions** mark goal states — see [Accepting conditions](accepting-conditions.md).

## State and identity

SEK identifies a state by a **canonical hash** of its serialized JSON. Canonical
hashing normalizes the structure so that states which are *equal as data* map to the
same node — for example, two collections with the same elements in a different order
are treated as the same state. This structural view of state is what lets models
with sets and dynamic objects explore into finite, well-behaved graphs.

Consequences worth knowing:

- Put all relevant state in public properties; anything not serialized is invisible
  to state identity (and will not be restored between steps).
- Prefer structural collections (`List<T>`, records) so set-equal states collapse.
- Keep the state *minimal* — only what's needed to decide guards and acceptance.

## Determinism

Exploration is deterministic: rules are considered in a stable order, parameter
domains are enumerated in order, and state hashing is canonical. The same model and
Cord always produce the same transition system, which makes diffs and CI meaningful.

## The model is not the implementation

A model program describes *intended behavior* abstractly. It is deliberately not
your production code. To check that the real system agrees with the model, use
[conformance](conformance.md), which replays the explored graph against a binding to
the system under test.

## Related

- [Rules and guards](rules-and-guards.md)
- [Accepting conditions](accepting-conditions.md)
- [State exploration](state-exploration.md)
- Guide: [Authoring a model](../guides/authoring-a-model.md)
