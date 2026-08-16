---
title: chat sample
description: A request/response chat protocol with per-user state, broadcast, and acknowledgement.
---

# chat

**Demonstrates:** a request/response protocol (MS-CHAT) with per-user protocol state
and broadcast message delivery.

- **Project:** `samples/chat`
- **Model:** `Chat.Model.ChatModel`

## What it covers

Users log on (`LogonRequest`/`LogonResponse`), broadcast messages that are queued to
all logged-on users (`BroadcastRequest`/`BroadcastAck`), and log off
(`LogoffRequest`/`LogoffResponse`). Each `User` carries a `UserState` enum and an
inbox. Object-typed parameters range over reachable users; user ids and payloads
come from Cord. The model is *accepting* when the protocol is quiescent (everyone
logged on, no pending broadcasts).

**Combined-slice result:** 57 states / 83 transitions / 17 accepting.

## Run it

```bash
dotnet build samples/chat/Model/chat.Model.csproj
sek explore CombinedSlices --project samples/chat
```

## Scenario slicing

`CombinedSlices` composes the logon/list, ordered-broadcast, and unordered-broadcast
scenarios after server startup. Each constituent slice uses `||` with `ModelProgram`.

```bash
sek explore LogOnOffListSlice --project samples/chat
sek explore BroadcastOrderedSlice --project samples/chat
```

## Related

- [Accepting conditions](../concepts/accepting-conditions.md)
- [Object domains](../concepts/object-domains.md)
