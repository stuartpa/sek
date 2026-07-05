# SEK — Cord Parity & Sample-Port Audit

_Generated 2026-07-05. Audits the SEK sample ports (`samples/`) against the original
Spec Explorer 2010 samples (`samples-source/`) and enumerates the remaining Cord language
gaps._

The goal of this document is to reach **perfect ports with no changes**. It therefore lists
every remaining deviation, its root cause, and what is required to eliminate it.

---

## 1. Executive summary

All nine samples explore and validate, and the six previously-simplified samples were
re-ported using the newly implemented Cord features (native domains, structs, derived-column
combinations, `Probability`, state slicing, `: fail` model checking, nested-slice flattening,
object-identity matching). **Two samples are byte-faithful** (`Operators`, `Sailboat`).
`RequirementReport` has no Cord (a post-processor) and is out of scope.

The remaining deviations fall into two buckets:

- **Architecture-driven** — SEK loads a *single* model type per project and models instance
  methods with an explicit target parameter. These force a handful of shape changes that are
  not yet removable without new engine capabilities (`action all`, model `scope`, container
  return types).
- **Cosmetic** — dropped switches, `static`/`abstract` modifiers, retuned bounds. These are
  removable immediately and do not affect exploration.

---

## 2. Remaining Cord gaps

Legend: ✅ implemented · ⚠️ approximated · ❌ not implemented.

### 2.1 Declarations & configuration

| Feature | Status | Notes |
|---|---|---|
| `action all <Adapter>` (import every action from a type) | ❌ | Parsed into `Configuration.ImportedActionTypes` but **never consumed** by the engine. Ports enumerate actions explicitly. |
| `construct model program … where scope = "Ns.Sub"` | ❌ (no-op) | SEK loads one model type per project; sub-namespace scopes are ignored. Blocks the original PG multi-scope layout. |
| Action return types (`action static Set<Account> …`, `bool …`) | ⚠️ | Return type is parsed but not modeled. `bool` is harmless; `Set<T>`/`Sequence<T>`/`Map<,>` returns are not first-class. |
| `static` / `abstract` action modifiers | ✅ parsed / ignored | Cosmetic — SEK rules are instance methods. |
| Switches: `GeneratedTestPath`, `GeneratedTestNamespace`, `TestClassBase`, `RecommendedViews`, `ProceedControlTimeout`, `DefaultParameterExpansionLimit`, `ForExploration` | ⚠️ | Parsed but ignored. `PathDepthBound` **is** honored (maps to max depth); `StateBound`/`StepBound` honored. |

### 2.2 Parameter generation & domains

| Feature | Status | Notes |
|---|---|---|
| `Condition.In`, `Condition.IsTrue` (Z3 + Roslyn fallback) | ✅ | |
| `Combination.Interaction / Pairwise / Isolated / Seeded / Expand` | ✅ | |
| Struct field domains (`Condition.In(info.Field, …)`), structured `bind` | ✅ | |
| Derived-column pairwise (`uint mon = days & 0x1; Combination.Pairwise(…, mon, …)`) | ✅ | |
| `[Flags]` enum flag columns (`days & DaysOfWeek.Mon`) | ✅ | |
| Native domains: ranges `a..b`, union `+`, `instances T`, `new T`, `{set}` | ✅ | |
| `T[n..m]` collection-by-size domains, maplets `{k -> v}` | ❌ | Not needed by any sample; not implemented. |
| `Probability.IsTrue(p)` with `if/else` | ⚠️ | Evaluated **deterministically** (majority branch, `p ≥ 0.5`). The `RandomSeed` switch is ignored; probabilistic sampling is not reproduced. |
| `let vars where {…} in Behavior` | ✅ | Predicate-only bounds use Z3; `Condition.In`/`Combination` bounds use the enumerative solver. |

### 2.3 Behavior algebra & scenarios

| Feature | Status | Notes |
|---|---|---|
| Full operator set (`;`, `\|`, `\|\|`, `\|\|\|`, `\|?\|`, `&`, `->`, `*`, `+`, `?`, `{n}`, `{n,}`, `{n,m}`, `_`, `...`, `!`) | ✅ | |
| Scenario slicing (`Scenario \|\| construct model program`) | ✅ | |
| Argument-pinned matching + per-arg `_` wildcard + object-id (`Id`/`Name`/`Handle`) | ✅ | |
| Scenario-supplied argument values | ✅ | |
| Nested-slice composition (`X \|\| (Y \|\| model)`) | ✅ | Flattened to `(X \|\| Y) \|\| model`. |
| State-slice preconstraint (`{. Type.Field = v; .}: M`) | ✅ | Sets a static field/property on a model-assembly type. |
| `: fail` model checking | ✅ | Fail states tracked NFA→DFA→product; reachable fail state = violation. |
| `StopAtError` (stop at first violation, return single trace) | ⚠️ | SEK reports *all* reachable fail states rather than halting at the first. |
| **Parameterized machines** with argument substitution (`machine AnyRequest(int id) { …(id)… }` used as `AnyRequest(5)`) | ❌ | Machine parameters are **not** substituted into the body. The SMB2 port inlines these. |
| `call` / `return` / `event` as **separate** atoms (call+return pair) | ⚠️ | `action event` parsed; events are single atoms. `_ ; _` works as two any-actions but not true call/return semantics. |
| Return-binding `X(args) / expr` | ⚠️ | The `/expr` is parsed and discarded; the return value is not bound. |

### 2.4 Constructs & reporting

| Feature | Status | Notes |
|---|---|---|
| `construct model program / accepting paths / bounded exploration / test cases` | ✅ | |
| `construct point shoot / accept completion` | ⚠️ | Steering approximated by exploring the target machine. |
| `construct requirement coverage where strategy / RequirementsToCover / MinimumRequirementCount` | ⚠️ | Reports the covered action set; requirement ids and `MinimumRequirementCount` are not tracked, so `ReqCoverage` and `MinimumReqCoverage` are not distinct. |
| Test strategies (`strategy = "shorttests" / "longtests"`) | ⚠️ | Parsed but ignored; `sek generate` uses its own transition-covering path. |

### 2.5 Microsoft.Modeling runtime types

| Feature | Status | Notes |
|---|---|---|
| `Set<T>` / `Sequence<T>` / `Map<K,V>` value semantics (structural equality for state hashing) | ❌ | SEK uses plain C# collections serialized to JSON for the state hash; there is no value-typed container runtime. |
| `TypeBinding` | ❌ | Not modeled. |
| `byte[]` / structured message payloads | ⚠️ | Represented as ordinary fields; no message-buffer modeling. |

---

## 3. Sample-by-sample audit

### 3.1 Operators — ✅ perfect port
Identical Cord to the original (all behavior-algebra operators). No changes.

### 3.2 Sailboat — ✅ effectively perfect
Keeps `bind`, `construct bounded exploration / accept completion / point shoot`, and test-case
construction. Only differences: dropped the generic type argument in `Condition.In<int>(…)` →
`Condition.In(…)` (works either way), and `where scope = "Sailboat.Model"` is a no-op.

### 3.3 RequirementReport — ➖ N/A
The original is an XSLT/C# post-processor with no Cord. No SEK analog.

### 3.4 ParameterGeneration — ⚠️ all 13 machines faithful in behavior, restructured in shape
- **Root cause:** the original uses one action name `AddJob` across **seven model scopes**
  (`scope="PG.ModelWithFrequency"`, `…WithStruct`, `…WithBitmask`, …). SEK has one model type,
  so the port uses **distinct action names** (`AddJob`, `AddJobStruct`, `AddJobBitmask`,
  `AddJobFlags`, `AddJobEC`, `CreateFile`) and drops `scope=`.
- `action abstract static void` → `action static void` (`abstract` dropped).
- `Let` original is a pure behavior `let … in A(x);B(y);C(z)`; the port slices it against the
  model (`(let …) || construct model program`) because SEK explores a behavior in a model
  project as the model.
- `Probability`: `"@^@\\"` → `"@^@"` (else-branch string, never taken); branch is deterministic,
  not seeded.
- Dropped `DefaultParameterExpansionLimit`, `RecommendedViews`, test-path switches.

### 3.5 Account — ⚠️ faithful behavior, several shape changes
- `action all AccountImpl` → explicit action list.
- `action static Set<Account> SearchAccounts(float)` → `action void SearchAccounts(float)`;
  the C# model returns `IEnumerable<Account>` (no `Set<Account>` value type).
- `static` dropped from action declarations.
- **Missing `TestSuite` machine** (`construct test cases for SlicedModelProgram`).
- Added `AccountExploration` / `SlicedAccount` alias machines not in the original.
- Machine base config `: Main` → `: ParameterCombinationConfig`.
- Bounds retuned (12800 → 500/2000); dropped `TestClassBase`/`GeneratedTestPath`/`GeneratedTestNamespace`/`PathDepthBound`.

### 3.6 atsvc — ⚠️ very close; two structural + cosmetic diffs
- `action all ATService` → explicit action list.
- State slice `{. ModelProgram.JobBound = 2; .}` → `{. AtsvcModel.JobBound = 2; .}` (qualifier
  renamed to the SEK model class).
- Dropped `generatedtestpath`, `TestClassBase`, `PathDepthBound`.
- Everything else (JobInfo struct + `out int jobId`, `bind`, validity predicate, trace pattern,
  test suite) is faithful.

### 3.7 chat — ⚠️ faithful behavior, restructured combination + dropped machine
- `action all ChatSetupAdapter; action all ChatAdapter` → explicit action list.
- `where scope = "Chat.Model"` dropped.
- `ListResponse` returns `Set<int>` in the original → `action void ListResponse()` (C# returns
  `IEnumerable<int>`).
- **`CombinedSlices` restructured**: original composes pre-sliced machines
  `(LogOnOffListSlice | BroadcastOrderedSlice | BroadcastUnorderedSlice)`; the port introduces a
  `CombinedScenario` and slices once. (Now that nested-slice flattening exists, the original
  form can be restored.)
- **`MinimumReqCoverage` machine dropped.**
- `ForExploration` flags dropped; bounds retuned.

### 3.8 PubSub — ⚠️ faithful; instance-target params added
- `action void Publisher.Publish(string data)` → `Publish(Publisher pub, string data)`; likewise
  `Subscriber.Received(string data)` → `Received(Subscriber sub, string data)`. SEK models an
  instance action with an explicit target parameter (its domain is the reachable objects).
- Consequently `Publish("object1")` → `Publish(_, "object1")`.
- Dropped `GeneratedTestPath`/`TestClassBase`; bounds retuned.
- `new`, `let`, and the parametrized slice + test suite are faithful.

### 3.9 SMB2 — ⚠️ faithful model-checking; protocol simplified + bounded
- `action all Smb2SetupAdapter; action all Smb2Adapter` → explicit action list; `scope` dropped.
- **Message fields simplified:** `CreateRequest(_,_,_,CreateType.Create,_)` (5 fields) →
  `CreateRequest(int msgId, CreateType type)`; `Read*`/`Write*` request/response actions dropped
  (the async demo only exercises Create/Close).
- **Added `Condition.In(msgId, 1, 2)` domains** so message-id generation is finite (the original
  draws ids from the protocol runtime).
- **`let` id range reduced 1..8 → 1..2** for tractable model-checking.
- Original **parameterized machines** `AnyRequest(int id)` / `AnyResponse(int id)` are **inlined**
  into `AsyncAntiScenario` (machine params are not substituted).
- State slice `{. Parameters.maxNoOfFiles = 2; .}` → `{. Smb2Model.maxNoOfFiles = 2; .}`.
- `StopAtError` present but SEK reports all fail states.
- The credit-window / outstanding-request semantics are a faithful **reconstruction**, not the
  original SMB2 message buffers.

---

## 4. Grouped list of modification types (across all ports)

**A. `action all` → explicit action lists** — Account, atsvc, chat, SMB2.
Cause: `action all` is parsed but not resolved to model rules.

**B. Model scopes collapsed to distinct action names** — ParameterGeneration (7 scopes → 6
action names), and `scope=` dropped (chat, SMB2, PG, Sailboat).
Cause: SEK loads one model type per project.

**C. Instance-method target parameters added** — PubSub (`Publish(Publisher pub, …)`,
`Received(Subscriber sub, …)`).
Cause: SEK models instance actions with an explicit target whose domain is the reachable objects.

**D. Container / return types dropped or void-ified** — Account `SearchAccounts` (`Set<Account>`
→ void + LINQ), chat `ListResponse` (`Set<int>` → void).
Cause: no `Set/Sequence/Map` value-type runtime.

**E. Domains added / bounded for finiteness** — SMB2 (`Condition.In(msgId, 1, 2)`; `let` ids
1..8 → 1..2; message fields reduced).
Cause: SEK needs an explicit finite domain where the original relied on the protocol runtime.

**F. Parameterized helper machines inlined** — SMB2 (`AnyRequest`/`AnyResponse`).
Cause: machine parameters are not substituted into the body.

**G. Pure behaviors wrapped as slices / restructured** — PG `Let` (`let … in A;B;C` →
`(let …) || model`), chat `CombinedSlices` (composed via `CombinedScenario`).
Cause: SEK explores a behavior in a model project as the model, so a `|| model` slice is needed.

**H. State-slice qualifier renamed** — atsvc (`ModelProgram.JobBound` → `AtsvcModel.JobBound`),
SMB2 (`Parameters.maxNoOfFiles` → `Smb2Model.maxNoOfFiles`).
Cause: the qualifier must name the SEK model class holding the static field.

**I. Dropped machines** — Account `TestSuite`, chat `MinimumReqCoverage`, SMB2 `Read`/`Write`
request/response.

**J. Cosmetic** — dropped `static`/`abstract` modifiers; dropped test-path/namespace/view
switches; retuned `StateBound`/`StepBound`; dropped generic `Condition.In<int>` type args;
`"@^@\\"` → `"@^@"`.

---

## 5. Path to perfect ports

Ordered by leverage. Items marked **(engine)** need a code change; **(port)** are pure Cord/model
edits that can be done now.

1. **(engine) Resolve `action all <Adapter>`** to model rules by label prefix. Removes Group A
   from Account, atsvc, chat, SMB2.
2. **(engine) Multiple model scopes per project** — load several model types and honor
   `scope="Ns.Sub"`. Removes Group B and lets PG use one `AddJob` name across scopes.
3. **(engine) Implicit instance-target domain** — allow `Publisher.Publish(string data)` where the
   target object is drawn from reachable objects implicitly. Removes Group C (PubSub).
4. **(engine) `Set/Sequence/Map` value types** with structural equality. Removes Group D and
   enables faithful `SearchAccounts` / `ListResponse` returns.
5. **(engine) Parameterized-machine argument substitution.** Removes Group F (SMB2
   `AnyRequest`/`AnyResponse`) and lets ids stay 1..8.
6. **(engine) Behavior machines explored directly** (not as the model) so pure `let … in …`
   works without a `|| model` wrapper. Removes Group G.
7. **(engine) Requirement-coverage tracking** (requirement ids, `MinimumRequirementCount`) and
   `StopAtError` single-trace. Restores chat `MinimumReqCoverage` and SMB2 stop-at-error.
8. **(engine) Seeded `Probability`** honoring `RandomSeed`. Makes PG `Probability` bit-faithful.
9. **(port, now) Restore dropped/cosmetic items**: add Account `TestSuite`; restore chat
   `CombinedSlices` to compose the pre-sliced machines (nested-slice flattening already supports
   this); re-add dropped switches and `static`/`abstract` for text fidelity; restore SMB2 full
   message fields + `Read`/`Write` once ids are unbounded.

Completing items 1–8 removes every architecture-driven deviation; item 9 closes the remaining
cosmetic gaps.
