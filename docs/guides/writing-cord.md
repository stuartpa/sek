---
title: Writing Cord scenarios
description: Use the Cord language to declare configurations, parameter domains, bounds, and behavior machines.
---

# Writing Cord scenarios

**Cord** is SEK's scenario and configuration language. It lives in `.cord` files
and does two jobs: it *configures* exploration (which actions, what parameter
domains, what bounds) and it *composes behavior* into machines. This guide is
practical; use the [Cord language reference](../reference/cord-language.md) for
implemented syntax and the [support matrix](../reference/cord-support.md) before
using advanced or legacy-compatible forms.

## Configurations

A `config` declares the actions in scope, parameter domains, and switches (bounds
and options). Configs can inherit with `:`.

```text
using MyApp.Model;

config Base
{
    action void Cart.AddItem(string sku, int qty);
    action void Cart.Checkout();

    switch StateBound = 500;
    switch StepBound  = 5000;
    switch TestEnabled = false;
}

config WithDomains : Base
{
    action void Cart.AddItem(string sku, int qty)
      where {.
        Condition.In(sku, "A", "B");
        Condition.In(qty, 1, 2, 3);
        Combination.Pairwise(sku, qty);
      .};
}
```

Inside a `where {. ... .}` block you can use:

- `Condition.In(param, v1, v2, ...)` — the parameter's candidate values.
- `Condition.IsTrue(expr)` — a boolean predicate that prunes combinations (Z3).
- `Combination.Pairwise(...)` — 2-wise coverage instead of the full product.
- `Combination.Interaction(...)` — full product (the default).

See [Parameter generation](../concepts/parameter-generation.md) and
[Combination strategies](../concepts/combination-strategies.md).

## Switches (bounds and options)

| Switch | Meaning |
|---|---|
| `StateBound` | Maximum number of states before exploration stops. |
| `StepBound` | Maximum number of transitions. |
| `PathDepthBound` | Maximum path depth from the initial state. |
| `StopAtError` | Stops expansion after the first reached model-check fail state. |
| `RandomSeed` | Reproducibly orders probabilistic branch domains. |

`TestEnabled` and `ForExploration` are informational. Legacy generated-output and
UI/view switches do not control SEK; use CLI options instead.

## Machines

A `machine` names a behavior to explore. The most common form asks SEK to explore
the underlying model program:

```text
machine Explore() : WithDomains
{
    construct model program from WithDomains
}
```

## Direct model and proof roles

Keep a direct, unsliced model-program machine even when adding focused scenarios:

```text
machine ModelProgram() : WithDomains
{
  construct model program from WithDomains
}
```

The direct machine explores complete reachable model behavior and can produce
model-derived rejection tests. A scenario slice proves a different property: which
legal traces remain under the scenario. Current sliced exploration does not emit
model-derived negative transitions, so do not replace the direct machine with a slice.

You can also compose **behavior** directly over abstract actions using the Cord
operator algebra (this is *behavior mode* — no model program required):

```text
machine Party() : PartyActivities
{
    ( (Dance; Sing) | (Eat; Drink) ) ; KeepPartying?
}
```

### Behavior operators

| Operator | Meaning |
|---|---|
| `;` | tight sequence |
| `\|` | choice (union) |
| `?` `*` `+` | optional / zero-or-more / one-or-more |
| `{n}` `{n,}` `{n,m}` | bounded repetition |
| `\|\|` `\|\|\|` `\|?\|` | synchronized / interleaved / sync-interleaved parallel |
| `->` | loose sequence |
| `&` | permutation |
| `_` | any single action |
| `...` | any sequence (`_*`) |
| `!` | negation of an atomic action |

The [Operators sample](../samples/operators.md) demonstrates every one.

> [!CAUTION]
> Keep `||`, `|||`, and `|?|` at the current/root composition. A parallel node
> nested beneath sequence, choice, repetition, permutation, or loose sequence can
> compile as empty. Also verify same-label interleavings explicitly.

## Scenario slicing

Compose a scenario with a model program to **slice** it — explore only the model
behaviors whose action sequence the scenario permits:

```text
machine AddManageScenario() : Config
{
    AddJob; (GetJobInfo | DeleteJob)*
}

machine ManagedJobs() : Config
{
    AddManageScenario || construct model program from Config
}
```

`sek explore ManagedJobs` explores the model but keeps only runs that start with an
`AddJob` and then only query or delete — the scenario acts as a filter over the full
model. A combined state is accepting when the model state is accepting **and** the
scenario has completed. Matching is by short action label, so keep short labels unique.
A non-empty invocation pins arguments (`AddJob("x", 600000)`), while `_` is a
per-argument wildcard and bare `AddJob`/`AddJob()` matches any arguments. See the
[atsvc sample](../samples/atsvc.md).

## Machine-local domains with `bind`

```text
machine SmallInputs() : Config
{
  bind AddJob({1, 2}, {"x", "y"})
  in
  construct model program from Config
}
```

Use `bind` at the top level of a model-backed machine. Supported atoms include `_`,
literals, `{set}`, integer ranges, union `+`, `instances T`, and structured
`Type(Field=domain, ...)`. A concrete bind replaces the action's extracted constraints;
it does not intersect them. Inspect emitted arguments.

## Shared finite values with `let`

```text
machine DifferentIds() : Config
{
  let int requestId, int responseId
    where {.
      Condition.In(requestId, 1, 2, 3);
      Condition.In(responseId, 1, 2, 3);
      Condition.IsTrue(requestId != responseId);
    .}
  in
  Request(requestId); Response(responseId)
}
```

Keep `let` domains explicitly finite and verify expansion produced assignments. A
zero-row `let` can leave the unsubstituted behavior in place.

## Pairwise and probabilistic ordering

`Combination.Pairwise` can include derived bit/flag expression columns. For a
prioritized but complete domain branch:

```text
action void File.Create(string name)
where {.
  if (Probability.IsTrue(0.8))
    Condition.In(name, "normal-a", "normal-b");
  else
    Condition.In(name, "rare-error");
.};
switch RandomSeed = 2;
```

SEK unions both branches; probability and seed order them reproducibly. This is not
statistical sampling. Verify both domains appear, especially near a bound.

## Test paths, steering, and model checking

```text
machine TestSuite() : Config where TestEnabled = true
{
  construct test cases where strategy = "shorttests" for ManagedJobs
}

machine CompletingPaths() : Config
{
  construct accepting paths for ModelProgram
}
```

`shorttests` selects many short witnesses; `longtests` selects fewer covering tours.
Accepting paths and other constructs should target model-backed machines.

Bounded exploration, point-shoot, accept-completion, and requirement coverage are
available with the restrictions in the [support matrix](../reference/cord-support.md).
For point-shoot, use model-backed phases and a simple Boolean goal field/property/method,
then verify nonzero expected phase and goal counts.

Model checking uses `: fail`:

```text
config ModelCheck : Config { switch StopAtError = true; }
machine ForbiddenOrder() : ModelCheck { ...; Close; Write : fail }
machine CheckOrder() : ModelCheck { ForbiddenOrder || ModelProgram }
```

Treat every reached fail state as an external hard failure. `StopAtError` truncates
search; it does not guarantee a failing `sek explore` exit code.

## Explore it

```bash
sek explore Explore --project path/to/project
sek explore Party   --project path/to/project   # behavior mode
```

## Tips

- Put shared actions and bounds in a base `config`; add domains in a derived one.
- Prefer explicit actions. If using `action all`, verify it resolves exactly the
  intended `[Rule]` set; a no-match path can expose all rules.
- Give every scalar parameter a finite domain and inspect generated arguments.
- If exploration hits a bound, tighten the scenario or reduce domains rather than
  raising bounds — an unbounded graph usually means the scenario is under-specified.
- Keep action signatures in Cord aligned with your model rule labels; `sek validate`
  flags mismatches.
- Treat `sek validate` as necessary but not sufficient. Reject warnings, empty or
  suspicious graphs, missing actions, unexpected fail states, unmet requirements,
  and stale generated artifacts.
