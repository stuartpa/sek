# COV<NNN>: <short title>

- **Created:** <date>
- **Phase:** <line-driving | functional | COMPLETE>
- **Status:** OPEN | COMPLETE
- **Readiness Gate:** PASS | FAIL — NOT READY

## Coverage (whole product)

| Metric | Before | After | Target |
|---|---|---|---|
| Line | | | 95%+ |
| Branch | | | 95%+ |
| Suite runtime | | | (fast) |

## Readiness Inventory

> One row per module of the product — every `components/*` component AND the vertical. A module
> with no tests is `Line 0% / FAIL`; no module may be omitted. The gate PASSES only if every row
> passes its class's criteria (all: ≥95% line/branch + conformant + green; **component** →
> unit/property, no MDL/CRD; **vertical** → MDL + CRD generating conformance, and domain-only).

| Module | Class | MDL? | CRD? | Line% | Branch% | Conformant? | PASS/FAIL |
|---|---|---|---|---|---|---|---|
| components/<Name> | component | n/a | n/a | | | | |
| <vertical module> | vertical | | | | | | |

## Remaining gaps

| File / behavior | Line/branch | Missing behavior | Action |
|---|---|---|---|
| | | | extend CRD<NNN> / add MDL<NNN> / justify |

## Deliberately uncovered (with rationale)

- <file:line> — <why it is acceptable to leave uncovered>

## Readiness Gate verdict

- [ ] **PASS** — every inventory row passes; product is READY FOR INCIDENTS.
- [ ] **FAIL** — NOT READY. Failing rows: <module: reason>. Next: `/speckit.engloopkit.model` or
  `/speckit.engloopkit.explore` on the largest gap.

> "Ready for incidents" may be stated ONLY when this verdict is PASS (PM001 anti-narration rule).

## Related

- Explorations: CRD<NNN>, …
- Models: MDL<NNN>, …
