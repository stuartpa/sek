# CRD<NNN>: <short title>

- **Created:** <date>
- **Targets model:** MDL<NNN>
- **Driven by gap:** <COV<NNN> | initial>
- **Script:** <path to .cord>
- **Status:** EXPLORED

## Coverage goal

<Which lines/branches or behaviors this exploration exists to cover.>

## Scenarios

<The paths through the model this explores, and why they were chosen (prefer few
high-coverage explorations over many overlapping ones).>

## Bounds

- Depth:
- Scenario argument domains:
- Rationale for bounds (keeps the run bounded and fast):

## Exploration result

| Metric | Value |
|---|---|
| States explored | |
| Transitions | |
| Goal states | |
| Run time | |

## Generated tests

- Location: <path>
- Count: <n>
- All green: <yes/no>   Suite time: <t>

> If any generated test failed, that is a real finding: either fix the implementation
> (incident-worthy) or fix MDL<NNN>. Never delete a failing generated test to go green.

## Related

- Coverage report: COV<NNN>
