---
title: Object domains
description: How reference-typed rule parameters draw their domain from the objects reachable in the current state.
---

# Object domains

Many models create objects dynamically — accounts, subscribers, files, sessions —
and then act on them. SEK supports this with **reachable-object domains**: a
reference-typed rule parameter automatically ranges over the objects of that type
that exist in the current state. This mirrors Spec Explorer's "live object" domains
and needs no annotation.

## How it works

For a parameter whose type is a class (not `string`), with no `[Domain]` and no Cord
`Condition.In`:

1. SEK walks the model state from the model instance — through public properties,
   fields, and enumerables — and collects every object assignable to the parameter's
   type (distinct by reference). This is the parameter's domain in that state.
2. Each candidate is represented internally by its **index** in that deterministic
   reachable list.
3. When a rule fires, the index is **materialized** against the *fresh copy* of the
   state that the rule mutates — so the object the rule receives is part of the very
   state being transformed, and mutations land correctly.

That last point matters: exploration invokes a rule on a fresh snapshot each time.
Resolving object parameters by index (rather than by a stale reference from a
different snapshot) is what makes `sub.Messages.RemoveAt(0)` or `account.Balance = …`
actually affect the successor state.

## Example

From the [Account sample](../samples/account.md):

```csharp
public List<Account> Accounts { get; set; } = new();

[Rule("AccountImpl.CreateAccount")]
public void CreateAccount()
{
    Require(Accounts.Count < 2, "bound the number of accounts");
    Accounts.Add(new Account { Balance = 0 });
}

[Rule("AccountImpl.SetBalance")]
public void SetBalance(Account account, int balance) => account.Balance = balance;
```

- `CreateAccount` grows the object domain.
- `SetBalance`'s `account` parameter ranges over the accounts currently in state;
  `balance` comes from Cord `Condition.In(balance, 10, 100)`.

Exploring `AccountExploration` yields exactly the ten structurally-distinct states
`{ [], [{0}], [{0,0}], [{10}], [{100}], [{0,10}], [{0,100}], [{10,10}], [{10,100}],
[{100,100}] }` with 58 transitions — the object domain and Z3 value domain combining
correctly.

## Mixing object and value parameters

A rule can mix reference and value parameters (e.g. `SetBalance(Account, int)`). SEK
enumerates the object domain directly and, for the value parameters, uses their Cord
domains (and pairwise/predicate strategies). Object- and floating-point-typed
parameters are enumerated rather than sent to Z3.

## Structural identity

Because state identity is a [canonical hash](model-programs.md#state-and-identity),
two objects with the same field values are the *same* as data — so `[{0},{0}]` is a
single, well-defined state, and object graphs explore into finite graphs.

## Related

- [Parameter generation (Z3)](parameter-generation.md)
- [Model programs](model-programs.md)
- Samples: [Account](../samples/account.md), [PubSub](../samples/pubsub.md)
