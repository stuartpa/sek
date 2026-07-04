---
title: Project configuration
description: The .specexplorerkit/config.json project descriptor — model, cord, binding, and out.
---

# Project configuration

A SEK project is a directory containing a `.specexplorerkit/config.json` descriptor.
It tells `sek` where the model assembly, Cord scripts, optional SUT binding, and
output directory are.

## Schema

```json
{
  "model":   { "assembly": "Model/bin/Debug/Model.dll", "type": "MyApp.MyModel" },
  "cord":    "Model",
  "binding": { "assembly": "Adapter/bin/Debug/Adapter.dll", "namespace": "Adapter" },
  "out":     ".specexplorerkit/out"
}
```

| Field | Required | Meaning |
|---|---|---|
| `model.assembly` | for model-mode | path (relative to the project dir) to the built model assembly |
| `model.type` | for model-mode | fully-qualified model type deriving from `ModelProgram` |
| `cord` | yes | directory (or file) containing the `.cord` script(s) |
| `binding.assembly` | for `sek test` | path to the adapter/SUT assembly |
| `binding.namespace` | for `sek test` | namespace exposing one method per action label |
| `out` | optional | output directory for `.seexpl` files (default `.specexplorerkit/out`) |

## Model mode vs behavior mode

- If `model.type` is set, `sek explore` runs in **model mode** — it loads the model
  assembly and explores the model program.
- If `model.type` is empty (or absent), machines are explored in **behavior mode**
  — pure Cord behavior over abstract actions, no model assembly needed (as in the
  [Operators sample](../samples/operators.md)).

## Paths

All paths are resolved **relative to the project directory** (the folder passed via
`--project`, or the current directory). Use forward slashes; they work on all
platforms.

## Assembly loading

When loading the model (and binding) assemblies, SEK adds their directories to the
resolver so their dependencies load too. If a model depends on another project's
output (e.g., an adapter that references a shared library), make sure both `bin`
directories are present after build.

## Example: the Account sample

```json
{
  "model": { "assembly": "Model/bin/Debug/Model.dll", "type": "AccountSample.AccountModel" },
  "cord":  "Model",
  "out":   ".specexplorerkit/out"
}
```

## Example with a binding (conformance)

```json
{
  "model":   { "assembly": "Model/bin/Debug/Model.dll", "type": "MyApp.MyModel" },
  "cord":    "Model",
  "binding": { "assembly": "Adapter/bin/Debug/Adapter.dll", "namespace": "Adapter" },
  "out":     ".specexplorerkit/out"
}
```

## Related

- [CLI reference](cli.md)
- Guide: [Running conformance](../guides/conformance.md)
