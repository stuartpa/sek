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

**Parameterized-slice result:** 90 states / 137 transitions / 8 accepting.

## Run it

```bash
dotnet build samples/PubSub/Model/PubSub.Model.csproj
sek explore TwoSubscribersWithParametersSlice --project samples/PubSub
```

## Scenario slicing

`TwoSubscribersSlice` constrains object creation and two publishes, exploring to
11 states / 13 transitions / 1 accepting state. `TwoSubscribersWithParametersSlice`
uses bounded `let` values for three publishes and explores to 90 / 137 / 8.

```bash
sek explore TwoSubscribersSlice --project samples/PubSub
```

## Related

- [Object domains](../concepts/object-domains.md)
- [State exploration → Bounds](../concepts/state-exploration.md#bounds)
