---
title: Running conformance
description: Bind a SEK model to a system under test and verify the implementation conforms to the model.
---

# Running conformance

*Conformance testing* replays an explored transition system against your real
implementation (the **system under test**, or SUT) and checks that the
implementation behaves as the model says it should.

## 1. Write a binding

A binding is an adapter assembly that exposes one method per model action label.
`sek test` reflects over the configured namespace, finds a method matching each
action, and calls it with the transition's arguments.

```csharp
namespace Adapter;

public static class AccountImpl   // namespace/type reachable in the binding assembly
{
    public static void CreateAccount() { /* call the real system */ }
    public static void SetBalance(object account, int balance) { /* ... */ }
}
```

## 2. Configure the binding

Add a `binding` block to `.specexplorerkit/config.json`:

```json
{
  "model":   { "assembly": "Model/bin/Debug/Model.dll", "type": "MyApp.AccountModel" },
  "cord":    "Model",
  "binding": { "assembly": "Adapter/bin/Debug/Adapter.dll", "namespace": "Adapter" },
  "out":     ".specexplorerkit/out"
}
```

## 3. Build and run

```bash
dotnet build Model/Model.csproj
dotnet build Adapter/Adapter.csproj
sek test AccountExploration --project path/to/project
```

```text
Explored 'AccountExploration': 10 states, 58 transitions.
Conformance against SUT (Adapter):
  transitions replayed : 58
  succeeded            : 58
  failed               : 0
  actions covered      : 4 (AccountImpl.CreateAccount, AccountImpl.SetBalance, ...)
TEST PASSED
```

## Interpreting results

- **`TEST PASSED`** — every explored transition was reproduced by the SUT.
- **`TEST FAILED`** — the report lists the first failing transitions. A failure is
  either an implementation bug or a model that no longer matches intended behavior.
  Decide which side is wrong and fix it.

## The flagship example

The **TPC-C** model shipped with SEK explores to 2,446 states / 12,706 transitions
and replays all 12,706 against a fake implementation with zero failures, covering
all ten transaction actions — a full end-to-end demonstration of the loop.

## Tips

- Keep the binding **thin**: translate an action + args into a single SUT call.
- Match method names to action labels (the part after the last `.` is the method;
  the type/namespace comes from the binding config).
- Use `sek explore` first to review the graph, then `sek test` to gate on conformance.
