---
title: Generating tests
description: Generate a runnable xUnit test project from a SpecExplorerKit exploration.
---

# Generating tests

`sek generate` turns an exploration into a **runnable xUnit test project**. Where
[conformance](conformance.md) replays the whole graph in one pass, test generation
emits a *reviewable, committable* suite of test methods — each a concrete scenario
(a path from the initial state to an accepting state) that drives your system under
test (SUT).

## Prerequisites

- A model + Cord that explore successfully (see [Authoring a model](authoring-a-model.md)).
- A **binding** to the SUT in `.specexplorerkit/config.json` (the generated tests call it).
- The `sek` tool installed.

## Generate

```bash
sek generate <machine> --project path/to/project
```

```text
Explored 'TpccExploration': 2446 states, 12706 transitions.
Generated 50 xUnit test(s) covering 667/12706 transitions.
Wrote .../TpccExplorationTests/TpccExplorationTests.cs
Run with: dotnet test ".../TpccExplorationTests"
```

Options:

| Option | Default | Meaning |
|---|---|---|
| `--out <dir>` | `.specexplorerkit/out/<machine>Tests` | output test-project directory |
| `--namespace <ns>` | `<binding-namespace>.Tests` | namespace for the generated tests |
| `--max <n>` | `50` | maximum number of test paths |
| `--solver z3\|enum` | `z3` | parameter solver used during exploration |

## What it produces

A self-contained test project:

```text
<machine>Tests/
├── <machine>Tests.csproj   # references xunit + the test SDK
├── <machine>Tests.cs       # one [Fact] per generated path/rejection
└── BindingAssets/          # binding DLL + built DLL/deps.json dependencies
```

Each `[Fact]` replays a path by calling the SUT binding for every action in the
sequence — for example:

```csharp
[Fact]
public void TpccExploration_Path01()
{
    _sut.Step("Warehouse.CreateWarehouse", "1");
    _sut.Step("District.CreateDistrict", "1", "1");
    _sut.Step("Customer.CreateCustomer", "1", "1", "1", "GC");
    // ...
}
```

An embedded harness resolves each action label to the corresponding binding method and
invokes it with the recorded arguments. SEK snapshots the built binding and its sibling
DLL/`.deps.json` dependencies into `BindingAssets`; the generated project copies those
assets into its output and loads only that snapshot. A failed call fails the test.

## Run

```bash
dotnet test path/to/<machine>Tests
```

There is no ambient binding fallback or `SEK_BINDING` override. Build the intended binding
before generation, regenerate after binding/dependency changes, and commit or transfer the
complete generated project including `BindingAssets`.

Model-derived negative transitions are emitted as tests that drive a legal prefix and then
assert that the SUT rejects the forbidden action.

## Coverage and path selection

SEK selects paths to cover the model's transitions: it repeatedly starts from the
initial state, walks a chain of still-uncovered transitions (routing between branches
as needed), and ends at the nearest accepting state. `--max` bounds the number of
tests; the command reports how many transitions the suite covers.

For large models (like TPC-C's 12,706 transitions) a bounded suite won't cover every
edge — tune `--max`, or generate per-scenario machines in Cord and generate tests for
each. For typical models, a small suite covers the whole graph.

Keep an unsliced model machine when rejection evidence is required; current scenario-sliced
exploration does not emit model-derived negative transitions.

## Related

- [Running conformance](conformance.md)
- [CLI reference: `sek generate`](../reference/cli.md#sek-generate)
