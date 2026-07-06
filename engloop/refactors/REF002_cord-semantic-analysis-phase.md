# REF002: Introduce the Cord semantic-analysis phase per ARC001

- **Date:** 2026-07-06
- **Cadence:** architect-driven convergence cycle (implementing ARC001)
- **Budget:** n/a (directed work under the ARC001/ARC002 mandate)
- **Status:** CHOSEN (implemented directly — see commit `63196a8`)
- **Emitted SEED:** none (implemented in-cycle)

## Signals gathered

| Signal | Finding |
|---|---|
| Recurring cause-classes (POSTMORTEM INDEX) | none recorded yet, but ARC001 notes that three historical bugs (dropped `//` comments, `action all` no-op, unbounded-`let` hang) all lived *between* parse and IR build — i.e. in the missing semantic phase |
| Architecture drift / boundary violations | **the core drift**: semantic analysis scattered across `Program.cs`/engine, no symbol table, no unified diagnostics (ARC001 §Context) |
| Duplicated business logic (DRY) | name-resolution derived on demand in several modules with no shared table |
| Hot spots (change frequency × complexity) | `Program.cs` (large, holds `ResolveModelScope`/`DesugarLet`/validate logic) |
| Test speed vs coverage | fine |

## Decision-tree branch taken

**Branch 2 — architecture drift / boundary violation.** ARC001 declares the front end must be a
proper compiler with a dedicated semantic-analysis phase + symbol table; today that phase is absent
and its responsibilities are scattered. Restoring this boundary protects the long-lived architecture
and is exactly where the recurring class of "silent drop / hang between phases" bugs originate — the
strongest structural signal, ahead of the component-leakage branch (addressed separately in REF001).

## Chosen refactor

Introduce `Sek.Cord/Semantics/` as a real phase 3:
- `Diagnostic` + `DiagnosticBag` (unified, collected, positioned diagnostics; codes `SEM001`–`SEM006`,
  plus `SEM100` for the reflection-level rule-mapping check).
- `SymbolTable` — the single source of truth for Cord names and per-machine effective facts.
- `SemanticModel` — the checked artifact the back end consumes.
- `SemanticAnalyzer.Analyze(document, targetMachine?)` — cross-reference checks the lexer/parser
  cannot see (duplicate config/machine, unknown base config, unknown construct reference, unknown
  target machine).

Wired in and load-bearing: `sek explore`/`test` analyze first and abort on errors with precise
diagnostics; `ExploreMachine` resolves imported types / declared actions / event actions **through
the `SemanticModel`**; `sek validate` migrated onto the same analyzer + shared diagnostic bag.

## Expected long-term benefit

Every Cord feature now has an obvious phase to live in; every diagnostic comes from the phase that
owns it (no more silent drops or hangs presented as mysteries); the scattered CLI logic collapses
toward one testable pass. New failing samples fail with a real, positioned diagnostic.

## Rationale for not choosing the others

- Branch 1: no recorded post-mortem cause-class yet (the phase pre-empts the class ARC001 identified).
- Branch 3 (component leakage): handled in REF001 this session.
- Branch 4/5/6: subsumed or not warranted.

## Hand-off

Implemented directly (ARC001 mandate). Guarded by 9 new `SemanticAnalyzerTests` + the 60-sample
regression gate (behaviour byte-identical for valid programs). Remaining migration (absorb
`ResolveModelScope`/`ActionImportResolver`/`DesugarLet`/`ConstraintExtraction`/`CollectReturnBindings`
behind the seam; name the optimization passes) recorded as REF-candidates in ARC001 for later cycles.
