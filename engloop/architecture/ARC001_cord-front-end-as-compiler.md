# ARC001: The Cord front-end is a compiler — phased, with a semantic-analysis stage and symbol table

- **Created:** 2026-07-06
- **Status:** ACCEPTED
- **Governs:** `Sek.Cord`, `Sek.Engine` (IR build), and the semantic-analysis boundary between them
- **Constitution ref:** architecture-guard (to be encoded); this ARC is the human-readable rule

## Decision

Cord is a language, so SpecExplorerKit **must process it as a compiler processes source code**:
through the classic, well-separated phases — **lexical analysis → syntax analysis → semantic
analysis (with a symbol table) → intermediate representation → optimization → back-end
(exploration / code generation)** — each phase with its own **error-handling** responsibility and a
clean artifact handed to the next. No phase may reach around another (e.g. the back end must not
re-parse text, and semantic decisions must not be scattered through the CLI).

This holds for years: every Cord feature is added by extending the *appropriate phase*, and every
Cord diagnostic is raised by the phase that owns it.

Reference model: the six standard compiler phases and their per-phase error handling
(lexical, syntax, semantic, intermediate-code, optimization, code generation) —
see *Phases of a Compiler* (GeeksforGeeks) and the Dragon Book (Aho/Lam/Sethi/Ullman).

## Context (from the bridging code)

The bridging implementation already has most phases, but **semantic analysis is scattered**, and
there is **no symbol table** and **no unified diagnostics**. Mapping the current code to the phases:

| Compiler phase | SpecExplorerKit today | State |
|---|---|---|
| **1. Lexical analysis** | `Sek.Cord/Lexing/CordLexer.cs`, `Token.cs` → token stream | ✅ clean phase |
| **2. Syntax analysis** | `Sek.Cord/Parsing/CordParser.cs` (recursive descent) → AST `Sek.Cord/Ast/Nodes.cs` | ✅ clean phase; raises `CordSyntaxException` |
| **3. Semantic analysis** | **`Sek.Cord/Semantics/`** — `SemanticAnalyzer` builds the `SymbolTable`, runs cross-reference checks, and produces a `SemanticModel` (`SymbolTable` + `DiagnosticBag`). Remaining resolvers (`Program.cs::ResolveModelScope`, `Sek.Engine/ActionImportResolver`, `DesugarLet`, `ConstraintExtraction`, `CollectReturnBindings`) are still called by the driver but are being pulled behind this phase's seam. | ✅ **dedicated phase + symbol table + unified diagnostics** (in place); ⏳ remaining resolver walkers migrating in |
| **4. Intermediate representation** | `Sek.Engine/BehaviorAutomaton.cs` (Thompson NFA → lazy subset DFA → product), `CompiledScenario`, and the `Sek.Core` transition-system IR (`ExplorationGraph`) | ✅ real IR; construction is sound but is fed by the scattered phase 3 |
| **5. Optimization** | lazy determinization, left-factoring of `let`, `MergeInConstraints` (probability union), DFA subset pruning | ✅ present, but ad-hoc / not named as a pass |
| **6. Back end** | exploration (`Sek.Engine/Explorer`), test generation (`Sek.Cli/TestGen`), renderers (`Sek.Core/Rendering`, `Seexpl`) | ✅ clean; consumes the IR only |

The evidence that phase 3 is the weak seam: symbols (config names, model scopes, action labels,
machine names, `let`/return-binding variables, parameter domains) are **resolved on demand, in
different modules, with no shared table** — which is exactly why bugs like the dropped `//`
comments, the `action all` no-op, and the unbounded-`let` hang lived in the cracks *between*
parsing and IR build. A world-class compiler resolves all of these once, in one phase, against one
symbol table, and reports precise diagnostics there.

## The rule

1. **Phase separation is mandatory.** `Sek.Cord` owns phases 1–2 (lex, parse) and produces an
   AST. A **semantic-analysis phase** (phase 3) consumes the AST and produces a **checked,
   resolved IR-input** (a *semantic model*: resolved configs, action universe, scoped model type,
   domains/predicates, desugared behavior, binding metadata). `Sek.Engine` owns phase 4+ (IR build,
   optimization, exploration) and consumes only the semantic model — **never raw text or the CLI**.
2. **A symbol table is the single source of truth for names.** Configs, machines, actions,
   parameters, `let`/return-binding variables, and scopes are entered into one symbol table during
   semantic analysis; every later lookup goes through it. No module re-derives a name resolution.
3. **Each phase owns its diagnostics.** Lexical → invalid tokens; syntax → `CordSyntaxException`;
   semantic → undeclared name / type mismatch / unbound domain / duplicate declaration, with
   source positions. Diagnostics are collected, not thrown ad-hoc, so a run can report *all* errors.
4. **Optimizations are named passes over the IR**, not side effects buried in the builder — so they
   can be enabled, tested, and reasoned about independently (determinization, left-factoring,
   domain union, dead-alternative pruning).
5. **The back end is pure w.r.t. the IR.** Exploration, test generation, and rendering consume the
   IR/semantic model and nothing upstream.

## Enforcement

- architecture-guard constitution article: **dependencies flow lexer → parser → semantic →
  IR → optimizer → back end**; back-end assemblies (`Sek.Cli` code-gen/render, `Sek.Engine`
  explorer) must not reference the lexer/parser text APIs; semantic decisions must live in the
  semantic-analysis phase, not in `Program.cs`.
- Review check: no name-resolution logic (`Resolve*Scope`, action/label resolution, `let`
  desugaring) outside the semantic-analysis phase once it exists.

## Consequences

- **Easier:** adding a Cord feature has an obvious home (which phase); every error gets a precise,
  positioned message from the owning phase; the scattered logic in `Program.cs` collapses into one
  testable pass; new samples that fail do so with a real diagnostic rather than a hang or a silent
  drop.
- **Constrained:** semantic logic may no longer live in the CLI or be duplicated across engine
  modules; introducing the semantic-analysis phase + symbol table is a **Stage 3 refactor target**
  (converge toward it — do not rewrite everything at once).

## Refactor tasks filed (converge in Stage 3 / refactor-scan)

- **DONE (Stage 3, first convergence):** introduced `Sek.Cord/Semantics/` — a real phase-3:
  - `Diagnostic` + `DiagnosticBag` (a unified, collected, positioned diagnostic vocabulary shared by
    the phase; codes `SEM001`–`SEM006` for Cord-level checks, `SEM100` for the reflection-level
    rule-mapping check).
  - `SymbolTable` — the single source of truth for Cord names (configs, machines, and the effective
    per-machine facts: imported action types, declared actions, event actions). Callers now resolve
    through it instead of re-deriving from the raw document.
  - `SemanticModel` — the checked artifact (`SymbolTable` + `DiagnosticBag`) the back end consumes.
  - `SemanticAnalyzer.Analyze(document, targetMachine?)` — runs cross-reference checks the lexer and
    parser cannot see: duplicate config/machine (`SEM001`/`SEM002`), unknown base config
    (`SEM003`/`SEM004`, warning), unknown `construct … for <ref>` (`SEM005`), unknown target machine
    (`SEM006`).
  - **Wired in and load-bearing:** `sek explore`/`sek test` run the phase first (`AnalyzeCord`) and
    abort on errors with precise diagnostics (instead of a hang or a silent drop); `ExploreMachine`
    now resolves imported types / declared actions / event actions **through the `SemanticModel`**
    (the single access point). `sek validate` was migrated onto the same `SemanticAnalyzer` + shared
    diagnostic bag (its former ad-hoc `problems` list is gone). 9 direct semantic-phase tests; 60-
    sample regression + full suite green (behaviour byte-identical for valid programs).
- **REF-candidate (remaining migration into phase 3):** absorb `ResolveModelScope`,
  `ActionImportResolver` (the reflection-dependent action-universe step), `ResolveMachineEventActions`
  short-name logic, `DesugarLet`, `ConstraintExtraction`, and `CollectReturnBindings` **behind** the
  `SymbolTable`/`SemanticModel` seam so the inheritance/desugar walkers no longer live in
  `CordDocument`/`Program.cs`/the engine. The access point is already centralized; the walkers move
  next, one per refactor cycle, keeping the regression green.
- **REF-candidate:** name the optimization passes explicitly (determinization, left-factoring,
  domain-union, pruning) behind an `IOptimizationPass` seam.

## Related

- SEED: SEED001 (Spec Explorer → SEK port); BRG001 (Cord implementation state), BRG002 (parity audit)
- Reference: *Phases of a Compiler* (GeeksforGeeks); Aho/Lam/Sethi/Ullman, *Compilers: Principles,
  Techniques, and Tools* (phases + symbol table + error recovery)
- Supersedes: none
