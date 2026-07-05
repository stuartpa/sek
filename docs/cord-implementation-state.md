# SEK — Cord Language Implementation State

_Last updated 2026-07-05._

This document records the **current state of the core Cord language implementation** in
SpecExplorerKit (SEK): what is implemented to production quality, what deeper semantics are
simplified to the depth the samples exercise, and which niche constructs remain unimplemented.

It is the authoritative "where are we" reference for the engine. For the historical port audit
see [cord-parity-and-sample-audit.md](cord-parity-and-sample-audit.md); for reference deltas when
porting from Spec Explorer see [porting-from-spec-explorer.md](porting-from-spec-explorer.md).

Legend: ✅ implemented (systems-engineering quality, unit-tested) · 🟡 implemented to sample
depth, deeper edge-semantics simplified · ❌ not implemented (no sample uses it).

---

## 1. Summary

- All nine sample ports use **byte-identical Cord** to the Spec Explorer 2010 originals and
  explore correctly in seconds (including the SMB2 model-checks at the full `id ∈ 1..8` range).
- The full behavior algebra, scenario slicing, parameter generation, constructs, model checking,
  and the eight "proper-engineering" features are implemented and covered by a **43-test xUnit
  suite** (`tests/Sek.Tests`).
- Baselines held throughout, including **TPC-C (2446 states / 12706 transitions / 1054
  accepting)**.
- The remaining gaps are **niche constructs no sample uses** and **two features implemented to
  sample depth** (documented below). There are **no misleading shortcuts** in the advertised
  feature set.

---

## 2. Fully implemented ✅

### 2.1 Declarations & configuration
- `action all <Adapter>` — resolved to the model rule set by label qualifier
  (`ActionImportResolver`); bare-labelled models import all rules.
- Explicit `action [static|abstract] [event|return|call] Ret Type.Method(params) [where {. .}]`.
- `construct model program … where scope = "Ns.Sub"` — loads the model type whose namespace is
  the scope (`ModelLoader.LoadModelTypeInScope`).
- Bound switches honored: `StateBound`, `StepBound`, `PathDepthBound`. `StopAtError` honored.

### 2.2 Parameter generation & domains
- `Condition.In`, `Condition.IsTrue` (Z3 with a Roslyn post-filter fallback).
- `Combination.Interaction / Pairwise / Isolated / Seeded / Expand`.
- Struct field domains (`Condition.In(info.Field, …)`) and structured `bind`.
- Derived-column pairwise (`uint mon = days & 0x1; Combination.Pairwise(…, mon, …)`) and
  `[Flags]` enum flag columns.
- Native domains: ranges `a..b`, union `+`, `instances T`, `new T`, `{set}`.
- `let vars where {. … .} in Behavior` — predicate-only bounds via Z3, `Condition.In` /
  `Combination` bounds via the enumerative solver. Comments in `where` blocks are stripped
  before statement extraction.
- `Probability.IsTrue(p)` — seeded by `switch RandomSeed` (`ProbabilityGate`): reproducible and
  seed-sensitive branch selection.

### 2.3 Behavior algebra & scenarios
- Full operator set: `;`, `|`, `||`, `|||`, `|?|`, `&`, `->`, `*`, `+`, `?`, `{n}`, `{n,}`,
  `{n,m}`, `_`, `...`, `!`.
- Scenario slicing (`Scenario || construct model program`), argument-pinned matching with per-arg
  `_` wildcard and object-identity (`Id`/`Name`/`Handle`), scenario-supplied argument values.
- Nested-slice composition (`X || (Y || model)` flattened) and state-slice preconstraint
  (`{. Type.Field = v; .}: M`).
- `: fail` model checking (fail states tracked NFA→DFA→product) with `StopAtError` halting at the
  first violation.
- Parameterized machines with argument substitution (`machine AnyRequest(int id) { …(id)… }`).

### 2.4 Constructs & reporting
- `construct model program / accepting paths / bounded exploration / test cases`.
- `construct point shoot / accept completion` — goal-directed steering (see §4.1).
- `construct requirement coverage` — tracks `Requirement.Capture(id)`; reports covered ids,
  `RequirementsToCover` hit/missing, and whether `MinimumRequirementCount` is met.
- Test strategies: `shorttests` (many short witnesses) / `longtests` (few long covering tours).

### 2.5 Runtime types
- `Set<T>` / `Sequence<T>` / `Map<K,V>` value types with structural equality, usable as **return
  values and as model state fields** (JSON converters round-trip them; sets/maps serialize in a
  canonical sorted order so the state hash is order-independent).

### 2.6 Action semantics
- Action kind (`call` / `return` / `event`) parsed and tracked; event transitions tagged as
  observations (`ActionInvocation.Kind`) and emitted as `Observe` (vs `Step`) in generated tests.
- Return-binding `Action(args) / var` — parsed and retained; return values captured onto the
  transition (`ActionInvocation.Result`).

---

## 3. Remaining language gaps ❌ (no sample uses these)

| Construct | State |
|---|---|
| `T[n..m]` collection-by-size domains | Not implemented (ranges `a..b` and `{set}` are). |
| Maplets `{k -> v}` | Not implemented. |
| `TypeBinding` | Not modeled. |
| Invocation-level `call` / `return` / `event` qualifier **inside a behavior** (e.g. `event Received` as a scenario atom) | Parsed and preserved but **not consumed** to tag the atom kind. The config-level `action event` **is** consumed. |
| `byte[]` / message-buffer payloads | Modeled as ordinary fields (a modeling convention, not a language construct). |
| Codegen / UI switches — `GeneratedTestPath`, `GeneratedTestNamespace`, `TestClassBase`, `RecommendedViews`, `ProceedControlTimeout`, `DefaultParameterExpansionLimit` | Parsed but not honored. Partly by design: SEK is CLI-first (`sek generate` uses `--out` / `--namespace` / `--max`; there is no VS UI). |

---

## 4. Implemented to sample depth, deeper semantics simplified 🟡

### 4.1 point-shoot / accept-completion
The goal-directed **steering** is real: the `with (. expr .)` state predicate marks goal states
and the graph is pruned to goal-reaching paths (`GraphAnalysis.FilterToReaching`);
accept-completion prunes to accepting-reaching paths. **Simplified:** the `Shoot = "…"` /
`Completer = "…"` machines are **not separately explored and appended as distinct continuation
phases** the way classic Spec Explorer composes them. The goal predicate + prune captures the
essential result and matches the samples.

### 4.2 Return-binding dataflow
Parsing, return-value capture, and the producer→consumer substitution component
(`ReturnBindingResolver`, unit-tested) all exist. **Simplified:** the resolver is **not yet wired
into the slice-exploration dataflow**, so `Producer()/h; Use(h)` in a live scenario slice does not
substitute `h` mid-exploration. No sample uses `/var`, so it is not exercised end-to-end.

### 4.3 Probability
Seeded and reproducible, but a **single branch draw per `where` block**, not a per-test-case
probabilistic *distribution*. Correct for exploration; not a randomized-sampling generator.

---

## 5. Test coverage

`tests/Sek.Tests` (xUnit) — **43 tests, all green**:

| Area | Tests |
|---|---|
| Probability seeded gate + branch selection | 7 |
| point-shoot steering + `GraphAnalysis.FilterToReaching` | 5 |
| Requirement capture + coverage aggregation | 4 |
| Containers as state fields (round-trip + structural hashing) | 6 |
| `action all` resolution + action-universe filtering | 6 |
| Test strategies (short/long) | 8 |
| Action kinds + return-binding resolver | 9 (+ smoke) |

Core algorithms live in libraries (`Sek.Core`, `Sek.Engine`, `Sek.Solver`, `Sek.Modeling`) with
public APIs so they are unit-testable, rather than in the CLI executable.

Run: `dotnet test tests/Sek.Tests/Sek.Tests.csproj`.

---

## 6. Path to full-grammar completeness

None of these are needed by the current samples:

1. Wire return-binding (§4.2) into the slice-exploration dataflow.
2. Compose the `Shoot` / `Completer` machines as explicit point-shoot phases (§4.1).
3. Implement `T[n..m]` collection-by-size domains and maplets `{k -> v}`.
4. Consume the invocation-level `call`/`return`/`event` qualifier.
5. Model `TypeBinding`.
6. Honor the codegen switches in `sek generate` (or document the CLI equivalents).
