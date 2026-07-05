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
equality. Return‑typed observations keep the original type (`Set<Account>` for
`AccountImpl.SearchAccounts`, `Set<int>` for chat `ListResponse`). **Model state fields must use a
serializable collection** (`List<T>`) — the value containers are for parameter/return values, not
JSON‑serialized state.

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

## 4. Known scalability limit — SMB2 model checking

`SMB2`'s `CheckAllSyncForNoAsync` / `CheckAsyncCreateCloseForNoAsync` use the anti‑scenario

```
let int id, int otherId where {. id ∈ 1..8; otherId ∈ 2..8; id ≠ otherId; .}
in ...; AnyRequest(id); AnyResponse(otherId) : fail
```

The leading `...` (`_*`) makes this an **unanchored search** over **56** argument‑pinned
alternatives. SEK slices via subset‑construction (NFA→DFA), which is exponential for this
unanchored/alternation pattern, so exploration at the full `id ∈ 1..8` range is compute‑bound.

- The **cord is byte‑identical** and every machine parses and compiles.
- The **logic is correct**: validated at a reduced id range the same machines give
  `CheckAllSync = 0` violations (sync mode has no out‑of‑order pair) and `CheckAsyncCreateClose > 0`
  violations (async mode does). `StopAtError` halts at the first violation.
- The **lifecycle** machines (`AllSync`, `AsyncCreateClose`, `AllSyncTwoFiles`,
  `AllSyncRequirementReduction`, `TestSuite`) explore within the sample's `StateBound = 4096`.

The clean fix (future work) is **on‑the‑fly / lazy subset construction** during slicing — track a
set of NFA states in the combined exploration state instead of pre‑determinizing — so only the
reachable subsets (bounded by `StateBound`) are materialized. This removes the determinization
blow‑up without any cord change.

---

## 5. Porting checklist

1. Create `.specexplorerkit/config.json` pointing at the built model assembly + default model type.
2. Author the model in C# against `Sek.Modeling` (`ModelProgram` + `[Rule]`/`[Domain]`/
   `[AcceptingCondition]`); use `List<T>` for state, `Set/Sequence/Map` for return values.
3. One `ModelProgram` subclass per `scope` namespace; instance actions take the receiver as the
   first parameter; static members back any state‑slice qualifiers.
4. Copy the `.cord` verbatim. Keep `using`/`action all`/`scope` as‑is.
5. `dotnet build` the model, then `sek explore <machine>`.
