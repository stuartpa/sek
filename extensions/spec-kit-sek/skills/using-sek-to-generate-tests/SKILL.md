---
name: using-sek-to-generate-tests
description: "Consume SpecExplorerKit (SEK) from a downstream project: scaffold a model and Cord project, configure the SUT binding, run validate/explore/test/generate, and diagnose binding or generated-replay failures."
user-invocable: false
---

# Use SEK from a downstream project

SEK explores a C# model plus Cord into a transition graph, replays it against a system under test
(SUT), and can generate a standalone xUnit project. Load
[`sek-cord-authoring`](../sek-cord-authoring/SKILL.md) before authoring `.cord` files.

## Project contract

A SEK project is a directory containing `.specexplorerkit/config.json`:

```text
MyProject/
├── .specexplorerkit/config.json
├── Model/
│   ├── MyProject.Model.csproj
│   ├── Model.cs
│   └── Config.cord
└── Sut/
    └── MyProject.Sut.csproj
```

```json
{
  "model": { "assembly": "Model/bin/Debug/net10.0/Model.dll", "type": "MyNs.MyModel" },
  "cord": "Model",
  "binding": { "assembly": "Sut/bin/Debug/net10.0/MySut.dll", "namespace": "MyNs.Sut" },
  "out": ".specexplorerkit/out"
}
```

- `model.assembly` and `model.type` identify the built `ModelProgram`.
- `cord` identifies the directory containing `*.cord`.
- `binding` identifies the built SUT assembly and namespace used by `test`/`generate`.
- `out` identifies generated transition-system output.

Missing or ambiguous paths, types, namespaces, methods, or bindings must fail; never substitute a
different assembly or stale artifact.

## Model project

Target `net10.0`, derive from `Sek.Modeling.ModelProgram`, and reference the
`SpecExplorerKit.Modeling` package matching the installed SEK release. In a source checkout, a
model can instead use a `ProjectReference` to `src/Sek.Modeling/Sek.Modeling.csproj`.

```csharp
using Sek.Modeling;

namespace MyNs;

public sealed class MyModel : ModelProgram
{
    [Rule("Workflow.Start")]
    public void Start(int id)
    {
        Require(id > 0, "id must be positive");
    }

    [AcceptingCondition]
    public bool Accepting() => true;
}
```

Rule labels follow `Class.Method`; generated replay resolves that class under the binding namespace
and invokes a matching method/arity.

## Command sequence

1. Build current model and SUT binaries.
2. Run `sek validate --project <project>`.
3. Run `sek explore <machine> --project <project>` and inspect graph counts/content.
4. Run `sek test <machine> --project <project>` for direct conformance.
5. Run `sek generate <machine> --project <project> --out <dir> --namespace <ns>`.
6. Run `dotnet test` on the generated project.

`explore` does not need a binding. `test` and `generate` do. Never reuse an old model DLL, binding
DLL, `.seexpl`, or generated suite after source changes.

## Generated replay contract

The generated project is standalone `net10.0`/xUnit and includes a reflection harness. For each path:

- one SUT instance per reflected type is reused across all steps in that test path;
- labels resolve to `<binding namespace>.<Class>` and `Method`;
- strings, enums, and primitive-like arguments are converted from recorded values;
- `call` actions become `Step`; config-level events become `Observe`;
- model-derived negative transitions become expected-error steps.

Current limitations:

- `Observe` directly invokes the bound method; it is not an asynchronous observation channel.
- model-time `/` return binding does not capture and thread a dynamic SUT runtime return.
- object-valued arguments/returns may require a custom harness or primitive-keyed adapter.
- exact class, method, arity, and conversion mismatches fail at runtime.

The generated project snapshots the built binding and sibling DLL/`.deps.json` dependencies under
`BindingAssets`, copies them into test output, and loads only that snapshot. There is no ambient
binding fallback or runtime override. Rebuild and regenerate after binding changes.

## Known-good references

- [Turnstile sample](https://github.com/stuartpa/sek/tree/main/samples/Turnstile) — minimal stateful SUT, model, Cord, binding, and replay pattern.
- [Conformance guide](https://stuartpa.github.io/sek/guides/conformance.html) — live replay workflow.
- [Generating tests](https://stuartpa.github.io/sek/guides/generating-tests.html) — generation and path selection.
- [Project configuration](https://stuartpa.github.io/sek/reference/project-config.html) — complete descriptor.
- [Cord reference](https://stuartpa.github.io/sek/reference/cord-language.html) and
  [support matrix](https://stuartpa.github.io/sek/reference/cord-support.html).

## Failure-closed checklist

- Build model and SUT before every proof run.
- Verify the configured model type and binding namespace exist in the intended assemblies.
- Keep rule labels and SUT methods exact and unambiguous.
- Require finite Cord domains and inspect emitted arguments.
- Reject bound hits, empty/suspicious graphs, missing actions, stale outputs, or zero generated tests.
- Use an unsliced model machine when model-derived rejection evidence is required.
- Use a custom harness rather than pretending unsupported asynchronous events, dynamic handles, or
  object conversion are covered.
