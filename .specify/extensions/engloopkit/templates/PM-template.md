# PM<NNN>: <short title>

- **Date:** <date>
- **Duration:** <HH:MM total across incidents>
- **Covers incidents:** IN<..>, IN<..>
- **Status:** COMPLETE

## Timeline

| Time | Event |
|---|---|
| | |

## Root causes

### Primary cause: <title>

- **What failed:** <system/component>
- **Why it failed:** <mechanism>
- **Why we didn't catch it:** <why verification/testing missed it>

### Contributing factor: <title, if any>

## Five whys

```
Symptom: <what users saw>
Why #1: Q: … A: …
Why #2: Q: … A: …
Why #3: Q: … A: …
Why #4: Q: … A: …
Why #5: Q: … A: …   (systemic level)
```

## ONE-AND-DONE analysis

For each root cause: abstract the concrete bug to its class, then a structural fix that
makes the class mechanically impossible.

- **Concrete bug:** …
- **Bug class:** …
- **Structural fix (mechanical, class-preventing, verifiable):** …

## Learnings

- **LRN001** — <class-level insight, never instance-level>

## Repair Items

> Each RPI must be specific enough to hand to `/speckit.engloopkit.repair`.

| RPI | Description (ONE-AND-DONE) | Size (tiny/full) | Spec/tinyspec | Status |
|---|---|---|---|---|
| RPI001 | | | (pending) | OPEN |

## Cause-class tags

<state-drift | dependency-failure | resource-exhaustion | bug-regression | deployment-incomplete | process-gap | validation-gap>

## References

- Incidents: docs/incidents/IN<..>.md
- Architecture: ARC<..>
- Recurrence of: <prior PM, if any>

## Approvals

- [ ] ONE-AND-DONE fixes reviewed for structural soundness
- [ ] Learnings accepted
- [ ] Repair Items routed via `/speckit.engloopkit.repair`
- [ ] Closed when all Repair Items verified in the target environment
