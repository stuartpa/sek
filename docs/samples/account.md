---
title: Account sample
description: Dynamic reachable-object domains — accounts created during exploration become the domain for later actions.
---

# Account

**Demonstrates:** dynamic [reachable-object domains](../concepts/object-domains.md)
and mixing object and value parameters.

- **Project:** `samples/Account`
- **Model:** `AccountSample.AccountModel` — a `List<Account>` with create/set/get/clear

## What it covers

`CreateAccount` grows a set of `Account` objects. `SetBalance(Account, int)` and
`GetBalance(Account)` take an `Account` parameter whose domain is the accounts that
exist in the current state; the `balance` value comes from Cord
`Condition.In(balance, 10, 100)`.

Exploring `ModelProgram` yields exactly ten structurally-distinct states:

```text
[]  [{0}]  [{0,0}]  [{10}]  [{100}]  [{0,10}]  [{0,100}]  [{10,10}]  [{10,100}]  [{100,100}]
```

**Result:** 10 states / 78 transitions / 10 accepting.

## Run it

```bash
dotnet build samples/Account/Model/Account.Model.csproj
sek explore ModelProgram --project samples/Account
sek view samples/Account/.specexplorerkit/out/ModelProgram.seexpl --format html --out account.html
```

## Why it matters

This is the proof that reference-typed parameters draw their domain from live model
objects, and that the object index is *materialized against the mutated state* so
that `account.Balance = balance` actually changes the successor state — producing
distinct states rather than self-loops.

## Scenario slicing

`SlicedModelProgram` composes a lifecycle scenario with `ModelProgram` via `||`:
`(CreateAccount; (SetBalance | GetBalance | SearchAccounts)*; Clear) || ModelProgram`.
It restricts the full model (10 states / 78 transitions) to 11 product states /
55 transitions / 1 accepting state.

```bash
sek explore SlicedModelProgram --project samples/Account
```

## Related

- [Object domains](../concepts/object-domains.md)
- [PubSub sample](pubsub.md)
