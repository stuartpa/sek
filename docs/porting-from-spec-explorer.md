# Porting a Spec Explorer project to SEK

_This guide accompanies the SEK sample ports. Every sample's Cord script uses **identical
Cord‑language content** to the original Spec Explorer 2010 sample; only *references* differ, and
only where a clean SEK implementation requires it. This document lists those reference deltas so
the mapping from an original project to a SEK project is explicit and reproducible._

The engine changes that make the identical Cord work are all pure language features (no
special‑casing): implicit instance‑method receivers, model `scope` resolution, `action all`,
parameterized machines, the container value types, `|| model` distribution over the behavior
algebra, behavior‑direct exploration, `: fail` model checking with `StopAtError`, and the
argument‑pinned/derived‑column parameter machinery.

---

## 1. What stays identical

The **Cord language content** of every `*.cord` is byte‑identical to the original:

- all `config`/`machine` declarations, the behavior algebra, `where { … }` blocks;
- `bind`, `let`, `construct …`, `Combination.*`, `Condition.*`;
- `action all`, `scope = "…"`, `: fail`, parameterized machines, `Set<T>`/`Sequence<T>` return
  types, etc.

`Operators`, `Sailboat`, `atsvc`, `PubSub`, `chat`, `Account`, and `ParameterGeneration` are
**0‑diff** against `samples-source/`. `SMB2`'s cord is **0‑diff** as well.

---

## 2. Reference deltas (the only differences)

### 2.1 `using` directives
SEK resolves types from the **model assembly** named in `.specexplorerkit/config.json`, not from
the Cord `using` directives. The original `using Microsoft.Modeling;`, `using <Sample>.Adapter;`,
`using <Sample>.Implementation;` lines are **parsed and ignored**, so they are kept verbatim (no
change). When authoring a *new* SEK model in C#, replace `using Microsoft.Modeling;` with
`using Sek.Modeling;` (that is where `ModelProgram`, `[Rule]`, `[Domain]`,
`[AcceptingCondition]`, `Condition`, and the `Set/Sequence/Map` value types live).

### 2.2 The model/adapter binding lives in `config.json`, not the cord
Spec Explorer binds actions to an adapter/model project via the Visual Studio project. SEK uses
`.specexplorerkit/config.json`:

```json
{ "model": { "assembly": "Model/bin/Debug/Model.dll", "type": "<default model type>" },
  "cord": "Model", "out": ".specexplorerkit/out" }
```

`model.type` is the default `ModelProgram` subclass; `scope`d machines override it (see 2.4).

### 2.3 `action all <Adapter>`
`action all <Adapter>` is accepted verbatim. SEK derives the action vocabulary from the loaded
model program (the `[Rule("…")]` methods), so `action all` names an adapter for source
compatibility but does not need the adapter type to exist in the SEK assembly.

### 2.4 Model `scope` → namespace
`construct model program … where scope = "Ns.Sub"` selects the concrete `ModelProgram` subclass
whose **namespace** equals the scope. In `ParameterGeneration`, the original uses one action name
(`SUT.AddJob`) across seven scopes; the SEK model therefore provides one class per scope
namespace (`PG.ModelWithFrequency.SUT`, `PG.ModelWithStruct.SUT`, …). No cord change.

### 2.5 Instance‑method actions
`action void Publisher.Publish(string data)` (an instance method with an implicit receiver) is
supported directly: the SEK rule is `[Rule("Publisher.Publish")] void Publish(Publisher pub,
string data)` and the engine treats the leading parameter whose type matches the declaring type
(`Publisher`) as the implicit receiver, drawn from the reachable objects. Scenario arguments pin
the *visible* parameters (so `Publish("object1")` matches on `data`). No cord change.

### 2.6 Container value types
`Set<T>` / `Sequence<T>` / `Map<K,V>` are provided by `Sek.Modeling` with value (structural)
equality. They work as return‑typed observations (`Set<Account>` for `AccountImpl.SearchAccounts`,
`Set<int>` for chat `ListResponse`) **and as model state fields** — dedicated JSON converters
round‑trip them and serialize sets/maps in a canonical (sorted) order, so the state hash is
order‑independent for sets/maps and order‑sensitive for sequences. The SMB2 `Pending` outstanding‑
request list is a `Sequence<int>` state field.

### 2.7 State‑slice qualifier
`{. ModelProgram.JobBound = 2; .}:` and `{. Parameters.maxNoOfFiles = 2; .}:` set a static member
before exploration. SEK resolves the member on a model‑assembly type (`AtsvcModel.JobBound`,
`SMB2.Model.Parameters.maxNoOfFiles`). The cord qualifier is kept verbatim; the static member must
exist on a model type.

### 2.8 Model‑provided parameter domains
Where the original relied on a model default expansion (e.g. `ParameterGeneration`'s Expand scope),
the SEK model supplies the domains with `[Domain("Method")]` returning the candidate array. Where
the original relied on the protocol runtime to bound message ids (SMB2), the SEK model bounds them
with `[Domain]` (e.g. `msgId ∈ {1,2}`). These are model‑side details; no cord change.

---

## 3. Documented semantic approximations

These constructs parse and explore with the identical cord, but SEK's semantics are a documented
approximation rather than a bit‑for‑bit match of the Spec Explorer runtime:

- **`Probability.IsTrue(p)` / `RandomSeed`** — evaluated **deterministically** (the `p ≥ 0.5`
  majority branch) rather than via Spec Explorer's seeded RNG. Reproducible, but the chosen branch
  may differ from a specific seeded run.
- **`construct requirement coverage where strategy / RequirementsToCover / MinimumRequirementCount`**
  — explores the target and reports the covered action set; requirement‑id tracking is not modeled,
  so `ReqCoverage` and `MinimumReqCoverage` explore identically.
- **`construct point shoot` / `accept completion`** — steering is approximated by exploring the
  referenced target machine.
- **`construct test cases where strategy = "shorttests"/"longtests"`** — the strategy switch is
  accepted; `sek generate` uses a transition‑covering path selection.

---

## 4. SMB2 model checking

`SMB2`'s `CheckAllSyncForNoAsync` / `CheckAsyncCreateCloseForNoAsync` use the anti‑scenario

```
let int id, int otherId where {.
    // bounding ids to a reasonable range
    id ∈ 1..8; otherId ∈ 2..8;
    // imposing requirement for asynchronicity
    id ≠ otherId;
.}
in ...; AnyRequest(id); AnyResponse(otherId) : fail
```

Two engine behaviors make this port work with the **byte‑identical** cord:

- **Comments in `where` blocks.** C# line (`// …`) and block (`/* … */`) comments are stripped
  before the block is split into statements, so a comment preceding a `Condition.IsTrue`
  bound never swallows it. All three bounds are kept, so the `let` enumerates the intended
  ~49 `(id, otherId)` combinations rather than leaving a variable unbounded.
- **Scenario values as a parameter source.** A scenario‑pinned concrete argument (a `let` id,
  or a setup pin like `AssumeShareExists(1, ShareType.DISK)`) seeds the corresponding model
  parameter's domain; the scenario's own argument filter still admits only the pinned value.
  This is Spec Explorer's scenario‑as‑parameter‑source semantics.

Results match the original's expectations and complete in seconds:

- `CheckAllSyncForNoAsync` → **empty** (0 violations): sync mode (window = 1) has no
  out‑of‑order request/response pair.
- `CheckAsyncCreateCloseForNoAsync` → a **trace to a failure state** (> 0 violations): async
  mode (window = 2) admits an out‑of‑order pair. `StopAtError` halts at the first violation.

The `AllSync` / `AsyncCreateClose` lifecycle machines explore within the sample's bounds.
Slicing uses **lazy (on‑the‑fly) subset construction** — DFA states are materialized only as
the model‑driven exploration queries them — so the unanchored `...` prefix does not force a
full up‑front determinization.

---

## 5. Porting checklist

1. Create `.specexplorerkit/config.json` pointing at the built model assembly + default model type.
2. Author the model in C# against `Sek.Modeling` (`ModelProgram` + `[Rule]`/`[Domain]`/
   `[AcceptingCondition]`); use `List<T>` for state, `Set/Sequence/Map` for return values.
3. One `ModelProgram` subclass per `scope` namespace; instance actions take the receiver as the
   first parameter; static members back any state‑slice qualifiers.
4. Copy the `.cord` verbatim. Keep `using`/`action all`/`scope` as‑is.
5. `dotnet build` the model, then `sek explore <machine>`.
