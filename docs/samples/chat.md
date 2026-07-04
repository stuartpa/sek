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

**Result:** 1,000 states / 4,512 transitions / 4 accepting **(bound hit)**.

## Run it

```bash
dotnet build samples/chat/Model/chat.Model.csproj
sek explore ChatProtocol --project samples/chat
```

## Note on bounds

Like PubSub, broadcast queues are unbounded, so exploration reports `(bound hit)`.
Tighten the Cord scenario to focus on a finite conversation.

## Related

- [Accepting conditions](../concepts/accepting-conditions.md)
- [Object domains](../concepts/object-domains.md)
