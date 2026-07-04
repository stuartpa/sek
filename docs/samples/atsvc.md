---
title: atsvc sample
description: A task-scheduler protocol modeled as a job lifecycle with object domains and Z3-generated parameters.
---

# atsvc

**Demonstrates:** a protocol state machine — the AT Service task scheduler — modeled
as a job lifecycle.

- **Project:** `samples/atsvc`
- **Model:** `ATSvc.Model.AtsvcModel`

## What it covers

`AddJob(command, time)` creates a job; `GetJobInfo(Job)` observes one; `DeleteJob(Job)`
removes one. The `Job` parameters range over reachable jobs; `command`/`time` come
from Cord `Condition.In`, and a `Condition.IsTrue` predicate rejects invalid
combinations — mirroring the classic sample's parameter slicing.

**Result:** 10 states / 42 transitions / 10 accepting.

## Run it

```bash
dotnet build samples/atsvc/Model/atsvc.Model.csproj
sek explore JobScheduler --project samples/atsvc
sek explore ManagedJobs  --project samples/atsvc   # scenario-sliced
```

## Scenario slicing

`ManagedJobs` slices the model with a scenario — `AddJob; (GetJobInfo | DeleteJob)*`
composed with the model program via `||`. The full `JobScheduler` explores to 10
states / 42 transitions; the slice restricts it to 8 states / 15 transitions (runs
that add one job and then only query or delete it — never a second `AddJob`). See
[Writing Cord → Scenario slicing](../guides/writing-cord.md#scenario-slicing).

## Porting note

The classic sample used a `JobInfo` struct and `MapContainer` state. In the SEK
port the struct is flattened into value parameters (which the Z3 solver handles
directly) and the map becomes a `List<Job>`; the protocol shape is preserved. See
[Migrating from Spec Explorer](../guides/migrating-from-spec-explorer.md).

## Related

- [Object domains](../concepts/object-domains.md)
- [Parameter generation](../concepts/parameter-generation.md)
