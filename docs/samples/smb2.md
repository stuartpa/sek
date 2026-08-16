---
title: SMB2 sample
description: A session/tree/file protocol lifecycle modeled with object domains.
---

# SMB2

**Demonstrates:** a protocol lifecycle — establish a session, connect a tree, create
and write files, then tear everything down.

- **Project:** `samples/SMB2`
- **Model:** `SMB2.Model.Smb2Model`

## What it covers

`SetupConnectionAndSession` → `TreeConnect` → `Create(Tree)` → `Write(SFile)` /
`Close(SFile)` → `TreeDisconnect(Tree)` → `LogOff`. Trees and files are objects in
model state; the rules that act on them take a `Tree`/`SFile` parameter drawn from
the reachable objects. Guards enforce the lifecycle ordering. The model is
*accepting* when the session is fully torn down.

**AllSync result:** 15 states / 60 transitions / 5 accepting.

## Run it

```bash
dotnet build samples/SMB2/Model/SMB2.Model.csproj
sek explore AllSync --project samples/SMB2
```

## Porting note

The classic SMB2 sample is explicitly a *simplified* protocol model with credits,
sequence ids, and message types. The SEK port keeps the session/tree/file lifecycle
shape using object domains and lifecycle guards; adapter message types (SUT-side)
are out of scope for model exploration.

## Scenario slicing

`AllSync` composes a synchronous setup scenario with `StateMachine` via `||` and then
allows the remaining context behavior. `AsyncCreateClose` is the bounded asynchronous
create/close slice (24 states / 60 transitions / 5 accepting).

```bash
sek explore AsyncCreateClose --project samples/SMB2
```

## Related

- [Object domains](../concepts/object-domains.md)
- [Rules and guards](../concepts/rules-and-guards.md)
