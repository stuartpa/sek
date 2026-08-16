# Safe Cord authoring patterns

Replace labels with exact model `[Rule("Type.Method")]` labels and apply the main skill's checks.

## Direct finite model

```text
config Main
{
    action void Workflow.Start(int id);
    action void Workflow.Advance(int id, Mode mode);
    action void Workflow.Stop();
    switch StateBound = 1000;
    switch StepBound = 5000;
    switch PathDepthBound = 100;
}

config Domains : Main
{
    action void Workflow.Start(int id)
        where {. Condition.In(id, 1, 2, 3); .};
    action void Workflow.Advance(int id, Mode mode)
        where {.
            Condition.In(id, 1, 2, 3);
            Condition.In(mode, Mode.Fast, Mode.Safe);
            Combination.Pairwise(id, mode);
        .};
}

machine ModelProgram() : Domains
{
    construct model program from Domains
}
```

Retain this unsliced machine for complete reachability and model-derived rejection generation.

## Top-level lifecycle slice

```text
machine Lifecycle() : Domains { Start; Advance*; Stop }
machine LifecycleSlice() : Domains { Lifecycle || ModelProgram }
```

Keep `||` at the product root. Use `ModelProgram` for rejection evidence and `LifecycleSlice` for
focused legal traces; a slice does not currently emit model-derived negative transitions.

## Argument-pinned scenario

```text
machine SafeModeScenario() : Domains
{
    Start(1); Advance(1, Mode.Safe)*; Stop
}
machine SafeModeSlice() : Domains { SafeModeScenario || ModelProgram }
```

Bare `Advance`/`Advance()` matches any arguments; non-empty arguments pin, with `_` as wildcard.

## Pure behavior mode

```text
config Protocol
{
    action abstract static void Actions.Open();
    action abstract static void Actions.Send();
    action abstract static void Actions.Close();
}
machine ProtocolBehavior() : Protocol { Open; Send+; Close }
```

Explore directly. Do not hide pure behavior inside a `construct ... for` target.

## Root parallel forms

```text
machine Sync() : Protocol { Left || Right }
machine Interleave() : Protocol { Left ||| Right }
machine Mixed() : Protocol { Left |?| Right }
```

Never nest these under `;`, `|`, repetition, `&`, or `->`.

## Top-level bind

```text
machine SmallInputs() : Main
{
    bind Start({1, 2}), Advance({1, 2}, {Mode.Fast, Mode.Safe})
    in
    construct model program from Main
}
```

Bind replaces that action's constraints; it does not intersect them. Structured form is available:
`bind Submit(Request(Kind={Read, Write}, Size={1, 2}), out _) in ...`.

## Bounded let

```text
machine DifferentIds() : Domains
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

Verify expansion produced concrete non-empty assignments.

## Derived Pairwise and probability ordering

```text
where {.
    Condition.IsTrue(days >= 0 & days <= 127);
    uint monday = days & 0x1;
    uint tuesday = days & 0x2;
    Combination.Pairwise(name, monday, tuesday);
.};
```

```text
where {.
    if (Probability.IsTrue(0.8))
        Condition.In(name, "normal-a", "normal-b");
    else
        Condition.In(name, "rare-error");
.};
switch RandomSeed = 2;
```

Both probability branches are unioned; seed orders them reproducibly. Require both domains.

## Test paths and accepting paths

```text
machine TestSuite() : Domains where TestEnabled = true
{
    construct test cases where strategy = "shorttests" for LifecycleSlice
}
machine CompletingPaths() : Domains
{
    construct accepting paths for ModelProgram
}
```

Use `shorttests` for many short witnesses and `longtests` for fewer tours. Generate separately from
the unsliced machine when negative conformance is required.

## Bounded and point-shoot steering

```text
machine BoundedShoot() : Domains
{
    construct bounded exploration where PathDepth = 2 for Shoot
}
machine CompletableShoot() : Domains
{
    construct accept completion where Completer = "ShootCompleter" for
        construct bounded exploration where PathDepth = 2 for Shoot
}
machine PointAndShoot() : Domains
{
    construct point shoot
        where Shoot = "CompletableShoot", Completer = "ShootCompleter"
        with (. ModelNamespace.ModelType.GoalReached .)
        for Point
}
```

Use model-backed phases and a simple Boolean goal field/property/method. Require expected nonzero
phase and goal counts. Accept-completion currently prunes by acceptance; named completer semantics
are incomplete.

## Forbidden-trace model checking

```text
config ModelCheck : Domains { switch StopAtError = true; }
machine ForbiddenOrder() : ModelCheck { ...; Stop; Advance : fail }
machine CheckOrder() : ModelCheck { ForbiddenOrder || ModelProgram }
```

Treat every reached fail state as an external hard failure. `StopAtError` only truncates search.

## Requirement reporting

Call `Requirement.Capture("REQ-id")` from model rules, then:

```text
machine Requirements() : Domains
{
    construct requirement coverage
        where RequirementsToCover = "REQ-start,REQ-stop", MinimumRequirementCount = 2
        for ModelProgram
}
```

This reports aggregate IDs; it does not select generated tests by requirement.

## Events and returns

`action event void Workflow.Completed(int id);` is tagged as Observe, but generated Observe directly
invokes the method. `Create() / handle; Use(handle)` threads a model return while slicing, not a SUT
runtime return during generated replay. Use a custom harness for asynchronous events or dynamic
handles.

## Never copy these forms

- `action exclude`, legacy config `domain`/`bound`, or parenthesized legacy action domains.
- Nested parallel products or pure behavior inside `construct ... for`.
- Unbounded numeric/string parameters or zero-row `let`.
- Reflective state preconstraints as a substitute for modeled initialization.
- Behavior-level call/return/event semantics or dynamic SUT-handle replay.
- Collection-size domains, maplets, `TypeBinding`, or unverified legacy switches.
