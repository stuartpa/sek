---
title: Samples
description: The classic Spec Explorer 2010 sample suite, ported to SpecExplorerKit and validated.
---

# Samples

To demonstrate fidelity to Spec Explorer, the original Spec Explorer 2010 sample
suite has been ported to SEK and validated. Each sample lives under `samples/<name>`
in the repository; the originals are preserved under `samples-source/<name>` for
comparison.

| Sample | What it demonstrates | Result |
|---|---|---|
| [Operators](operators.md) | the full Cord behavior algebra | 18 machines explore (e.g. Party 6/6/3) |
| [ParameterGeneration](parameter-generation.md) | Z3 combination strategies + predicate pruning | Product 27, Pairwise 11, Constraint 21 |
| [Account](account.md) | dynamic reachable-object domains | 10 states / 58 transitions |
| [PubSub](pubsub.md) | pub/sub object model, message queues | 500 states (bound hit) |
| [atsvc](atsvc.md) | task-scheduler protocol | 10 states / 42 transitions |
| [chat](chat.md) | request/response chat protocol | 1000 states (bound hit) |
| [SMB2](smb2.md) | session/tree/file lifecycle | 17 states / 32 transitions |
| [Sailboat](sailboat.md) | stateful model + pairwise + state-dependent domains | 4000 states (bound hit) |
| [RequirementReport](requirement-report.md) | post-processing/reporting | HTML reports via `sek view` |
| [TPC-C](tpc-c.md) | large model + full conformance | 2,446 / 12,706; conformance PASSED |

## Reproduce

From the repository root:

```bash
dotnet build src/Sek.Cli/Sek.Cli.csproj
pwsh scripts/validate.ps1     # builds each sample model and explores it
```

Or run one:

```bash
sek explore <Machine> --project samples/<Name>
```

> [!NOTE]
> Some models (chat, PubSub, Sailboat) have intentionally unbounded state spaces;
> exploration reports `(bound hit)` when a `StateBound`/`StepBound` truncates the
> search — matching the classic samples' own bounds.
