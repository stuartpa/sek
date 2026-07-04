---
title: Rules and guards
description: How SpecExplorerKit rules define actions and how guards decide when an action is enabled.
---

# Rules and guards

## Rules are actions

A **rule** is a method marked `[Rule]` on a model program. Each rule is an *action*
the system can take. During exploration, SEK tries every rule in every reachable
state.

```csharp
[Rule("Cart.Checkout")]
public void Checkout()
{
    Require(Items.Count > 0, "cart is empty");
    Checked = true;
}
```

The **action label** is what appears on transitions and what Cord and bindings
refer to. It defaults to `DeclaringType.MethodName`; pass an explicit label to match
your Cord declarations, e.g. `[Rule("Cart.Checkout")]`.

## Guards enable or disable an action

A **guard** is a precondition. In a given state an action is *enabled* only if all
its guards hold. SEK expresses guards with `Require`:

```csharp
protected static void Require(bool condition, string reason);
```

If `condition` is `false`, `Require` throws `GuardDisabledException`, which the
explorer catches and interprets as *"this action is not enabled in this state"* —
the action simply produces no transition from that state. Any **other** exception is
reported as a diagnostic (it indicates a bug in the model, not a disabled action).

```csharp
[Rule("Turnstile.Push")]
public void Push()
{
    Require(!Locked, "still locked");   // disabled while locked
    Locked = true;
}
```

`Condition.IsTrue(condition, "reason")` is an equivalent spelling carried over from
Spec Explorer.

## Parameters

Rules can take parameters. Their candidate values (their *domain*) come from Cord
`Condition.In`, a `[Domain]` method, reachable objects, or the type's natural domain
— see [Parameter generation](parameter-generation.md) and
[Object domains](object-domains.md). For each combination of parameter values, SEK
invokes the rule on a fresh copy of the state and records the resulting transition
(unless a guard disables it).

## Effects

After the guards pass, the rule body mutates state to describe the action's effect.
The resulting state is snapshotted and de-duplicated by
[canonical hash](model-programs.md#state-and-identity).

## Guard patterns

- **Preconditions**: `Require(Balance >= amount, "insufficient funds")`.
- **Bounding the state space**: `Require(Items.Count < 3, "cap items")` keeps
  exploration finite.
- **Parameter validity**: `Require(atx == X, "must match current position")` pairs
  well with a state-dependent `[Domain]`.

## Related

- [Model programs](model-programs.md)
- [Accepting conditions](accepting-conditions.md)
- [Parameter generation](parameter-generation.md)
