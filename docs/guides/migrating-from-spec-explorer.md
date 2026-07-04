---
title: Migrating from Spec Explorer
description: Move classic Microsoft Spec Explorer projects (models, Cord, adapters) to SpecExplorerKit.
---

# Migrating from Spec Explorer

SEK is a clean-room revival of Microsoft Spec Explorer for modern .NET. If you have
classic Spec Explorer projects, this guide maps the old concepts to the new ones.

## What stays the same

- **Cord** is preserved as a first-class language — configurations, `action`
  declarations, `where` parameter blocks, switches, and the behavior operator
  algebra all carry over.
- The **workflow** is the same: model + scenario → explore → view → generate/verify.
- **Model/Adapter/Impl** separation is unchanged.

## What changes

| Classic Spec Explorer | SpecExplorerKit |
|---|---|
| Visual Studio extension + designer | CLI-first `sek` tool; no Visual Studio |
| `.NET Framework` + `Microsoft.Modeling` / `Microsoft.Xrt.Runtime` | .NET 8 + `Sek.Modeling` |
| `static` model classes with static state | a class deriving from `ModelProgram`, state in public properties |
| `[Rule]` on static methods | `[Rule("Label")]` on instance methods |
| `[AcceptingStateCondition]` | `[AcceptingCondition]` |
| `Condition.IsTrue(...)` as guards | `Require(cond, "reason")` (or `Condition.IsTrue(cond, "reason")`) |
| `Microsoft.Modeling` containers (`SetContainer`, `MapContainer`, `Sequence`) | plain `List<T>` / records; structural state hashing handles set-equality |
| `.seexpl` proprietary output + VS viewer | `.seexpl` JSON + `sek view` (Mermaid/DOT/HTML) |
| Parameter generation engine | Z3-backed solver (`Sek.Solver`) |
| Post-processing via `Microsoft.SpecExplorer.ObjectModel` | read `.seexpl` JSON directly, or `sek view` |

## Step-by-step

1. **Retarget the model project** to `net8.0` and replace the `Microsoft.Modeling`
   reference with `SpecExplorerKit.Modeling`.
2. **Convert the model class** to derive from `ModelProgram`. Move static state into
   public instance properties. Give it a parameterless constructor.
3. **Port rules.** Change static `[Rule]` methods to instance methods; keep the
   action label via `[Rule("Area.Action")]`. Replace guard calls with `Require(...)`.
4. **Port accepting conditions** from `[AcceptingStateCondition]` to
   `[AcceptingCondition]`.
5. **Replace modeling containers** with `List<T>`/records. Objects created during
   exploration become the domain for reference-typed parameters automatically
   (see [Object domains](../concepts/object-domains.md)).
6. **Keep your Cord** mostly as-is. Advanced scenario-control constructs (`bind`,
   `construct point shoot`, `construct bounded exploration`) are on the roadmap; the
   common `construct model program from <Config>` and the full behavior algebra work
   today.
7. **Add `.specexplorerkit/config.json`** pointing at the built model assembly, the
   Cord directory, and (optionally) the adapter binding.
8. **Validate and explore:** `sek validate` then `sek explore`.

## Worked examples

Every one of the nine classic Spec Explorer 2010 samples has been ported and lives
under `samples/`. Compare `samples-source/<name>` (the original) with
`samples/<name>` (the SEK port) to see the migration applied in practice. See
[Samples](../samples/index.md).

## Notes on parameter generation

Classic Spec Explorer derived some parameter domains from model-side code. In SEK,
value-parameter domains come from Cord `Condition.In` and are solved by Z3;
reference-parameter domains come from reachable objects; enum/bool parameters get
natural domains. Struct-typed parameters are typically flattened to their fields.
See [Parameter generation](../concepts/parameter-generation.md).
