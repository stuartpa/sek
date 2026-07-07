# ARC002: Component boundary — reusable components live in `components/`, the vertical composes them

- **Created:** 2026-07-06
- **Status:** ACCEPTED
- **Governs:** the whole repository — the split between `components/` and `src/`
- **Constitution ref:** architecture-guard (to be encoded); enforced per the EngLoopKit component pattern

## Decision

SpecExplorerKit is built as **a vertical that composes components**. Code that would be useful,
**unchanged, in a repo solving a totally different problem** (it wraps only the .NET runtime / BCL /
third-party libraries and carries **no SpecExplorerKit domain knowledge**) is a **component** and
lives as its own class-library project under **`components/`**, named
`SpecExplorerKit.Components.<Name>`. Everything that *is* SpecExplorerKit — the Cord language, the
model/exploration semantics, the `sek` CLI — is the **vertical** and stays under `src/`.

**Dependencies point one way: vertical → components (and component → component), never the reverse.**

## Context (from the bridging code)

The bridging code already contains several clearly-generic building blocks tangled into the
vertical assemblies. The litmus test (*useful, unchanged, in an unrelated repo?*) classifies them:

| Module (today) | Assembly | Classification | Why |
|---|---|---|---|
| `CanonicalJson` (JSON canonicalize + SHA-256 hash) | was `Sek.Engine` | **component** ✅ *extracted* | generic content hashing; no model/exploration concept |
| `ProbabilityGate` (seeded reproducible Bernoulli gate) | `Sek.Solver` | **component** ✅ *extracted* (`Components.Random`) | generic seeded RNG; no domain |
| `Combinatorics` (pairwise / interaction / isolated / seeded / expand over columns) | `Sek.Solver` | **component** ✅ *extracted* (in `Components.Solving`) | generic combinatorial test design |
| `GraphAnalysis` (reachability prune / merge-by-key) | `Sek.Core/Analysis` | **component** (candidate, needs a generic graph seam) | generic directed-graph algorithms; currently coupled to `ExplorationGraph` |
| NFA→DFA subset construction / product (the automaton core) | `Sek.Engine/BehaviorAutomaton` | **component** (candidate, hard) | generic finite-automata theory; entangled with scenario semantics |
| Z3 glue (`Z3Solver` translation), Roslyn predicate host | `Sek.Solver` | **component** ✅ *extracted* (`Components.Solving`) | generic "constraint spec → satisfying assignments" |
| Renderers (DOT / Mermaid / seexpl) | `Sek.Core/Rendering` | **component** (candidate) | generic graph rendering |
| Cord lexer/parser, AST, semantics | `Sek.Cord` | **vertical** | *is* the Cord language |
| `ModelProgram`, attributes, `Requirement`, exploration `Explorer`, `ModelIntrospector` | `Sek.Modeling`, `Sek.Engine` | **vertical** | *is* SpecExplorerKit's model + exploration domain |
| `sek` CLI, `TestGen`, `Conformance`, `ProjectConfig` | `Sek.Cli` | **vertical** | *is* the tool |

## The rule

1. A module that passes the litmus test **must** live in `components/<Name>` as
   `SpecExplorerKit.Components.<Name>` (its own project/assembly).
2. **Components carry no domain knowledge.** Domain specifics (e.g. what a "state" or an "action"
   is) are passed in by the vertical; a component may know the BCL and third-party libs only.
3. **One component, one folder, one job.**
4. **No component depends on the vertical.** A component may depend on another component.
5. The vertical (`src/…`) composes the components it needs.

## Enforcement

- architecture-guard: a dependency-direction rule — no `ProjectReference` from
  `components/**` to `src/**`; the review flags any generic (domain-free) code still living under
  `src/` as a refactor task.
- CI already builds `src/Sek.Cli` (which transitively builds every component) and runs the unit
  suite + the sample-exploration regression gate, so extractions are guarded.

## Consequences

- **Easier:** generic building blocks get isolated, independently-tested assemblies; the vertical's
  intent is clearer; components are liftable into other repos.
- **Constrained:** new generic code must go to `components/`, not be dropped into an engine
  assembly; extractions must preserve the one-way dependency rule.

## Progress & refactor tasks (converge in Stage 3 / refactor-scan)

- **DONE (pilot):** `CanonicalJson` extracted → `components/SpecExplorerKit.Components.Json`
  (`Sek.Engine` now references it; 5 direct component tests; regression gate green).
- **DONE:** `ProbabilityGate` extracted → `components/SpecExplorerKit.Components.Random`
  (a seeded reproducible Bernoulli gate; `Sek.Cord` now references it; the pre-existing
  probability tests exercise it directly). It carried no domain knowledge — a clean lift.
- **DONE:** the pure directed-graph reachability algorithms extracted →
  `components/SpecExplorerKit.Components.Graphs` (`Reachability.Backward`/`Forward` over abstract
  `(from, to)` edges with an equality comparer). `Sek.Core.Analysis.GraphAnalysis` is now the
  **domain adapter**: it projects `ExplorationGraph` transitions onto `(FromStateId, ToStateId)`
  edges and delegates the fixpoint/BFS to the component (this is the "generic graph seam" the ARC
  called for). 5 direct component tests; regression gate green.
- **DONE:** the entire constraint-solving backend extracted → `components/SpecExplorerKit.Components.Solving`
  (was the whole `src/Sek.Solver` project). It is a generic *constraint spec → satisfying
  assignments* engine + combinatorial test design (`SolverParam`/`SolverConstraint`/`Expr`/
  `CombinationSpec`, `PredicateEval`, `Combinatorics`, `EnumerativeSolver`, `Z3Solver`,
  `RoslynPredicate`, `Z3Probe`) — no SEK domain concept, so it moved wholesale. `Sek.Cord` and
  `Sek.Engine` reference it; 141 unit tests + 60-sample regression green after the move. This is a
  large step of the "vertical → domain-only" convergence (PM002): the solver was generic code
  living in the vertical.
- **REF-candidate (RESOLVED — kept vertical):** renderers (`DotRenderer`/`MermaidRenderer`/
  `HtmlRenderer`). Re-scan verdict below.
- Do **not** extract all at once — each refactor cycle pulls a little more out (per the component
  pattern's "converge toward" rule).

## Final re-scan (2026-07-07) — no residual generic code remains

A full re-scan of `src/**` (litmus test: *useful, unchanged, in an unrelated repo, carrying no
SpecExplorerKit domain knowledge?*) was performed. Every generic primitive is already extracted;
the residual candidates each **fail** the litmus test and are correctly vertical:

- **Renderers** (`DotRenderer`, `MermaidRenderer`, `HtmlRenderer`): they render the **domain IR**
  (`ExplorationGraph`/`ModelState`/`Transition`) using **domain conventions** — accepting state →
  `doublecircle`/`--> [*]`, the initial `__start` seam, edge labels = `Action.Display`. They are not
  usable *unchanged* elsewhere (they name the domain types directly). Introducing a generic
  graph-render seam for three 40–53-line renderers whose entire value is the domain-specific output
  would be over-engineering with no reuse payoff. **Kept vertical.**
- **`GraphAnalysis`**: already the domain adapter over `Components.Graphs.Reachability` (projects
  `ExplorationGraph` → abstract edges, delegates the fixpoint). The generic part is extracted; the
  remainder is domain graph-steering (accepting / point-shoot / goal). **Kept vertical.**
- **`BehaviorAutomaton` (NFA→DFA)**: entangled with scenario semantics (accepting conditions, action
  guards, action-kind matching); not usable unchanged as pure automata theory. **Kept vertical.**
- Confirmed by search: **no** stand-alone generic helpers (sort/hash/combinatorics/permutation/
  shuffle/topological/edit-distance) remain under `src/**` — all such primitives live in
  `components/`. Dependency direction verified one-way (no `components/** → src/**` ProjectReference).

**Verdict: ARC002 component boundary is satisfied.** No further extraction is warranted; new generic
code must still go to `components/`.

## Related

- Component pattern: EngLoopKit `docs/component-pattern.md`
- SEED001; BRG001/BRG002; ARC001 (compiler phases — its extracted phases will surface more components)
- Supersedes: none
