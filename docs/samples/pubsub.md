---
title: PubSub sample
description: A publish/subscribe object model with dynamic publishers, subscribers, and message queues.
---

# PubSub

**Demonstrates:** dynamic object creation and object domains over a graph of
publishers and subscribers with per-subscriber message queues.

- **Project:** `samples/PubSub`
- **Model:** `PubSub.Model.PubSubModel`

## What it covers

Publishers and subscribers are created during exploration. `Publish(Publisher, msg)`
fans a message out to all of a publisher's subscribers; `BroadcastAck(Subscriber)`
consumes the head of a subscriber's queue. Object-typed parameters (`Publisher`,
`Subscriber`) range over reachable objects; message payloads come from Cord.

**Result:** 500 states / 1,610 transitions / 4 accepting **(bound hit)**.

## Run it

```bash
dotnet build samples/PubSub/Model/PubSub.Model.csproj
sek explore PubSubExploration --project samples/PubSub
```

## Note on bounds

The message queues are unbounded (you can always publish again), so the state space
is infinite; exploration reports `(bound hit)` when `StateBound` truncates it — the
classic PubSub sample notes the same "infinite, pruned" behavior. Tighten the Cord
scenario to explore a specific finite slice.

## Related

- [Object domains](../concepts/object-domains.md)
- [State exploration → Bounds](../concepts/state-exploration.md#bounds)
