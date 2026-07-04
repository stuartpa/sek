# `/speckit.sek.model`

Generate a **SpecExplorerKit (SEK) model program** and **Cord** scenarios from the
current feature's specification, so the behavior described in `spec.md` can be
explored and verified as a finite-state model.

## Prerequisites

- The `.NET 8` SDK is installed (`dotnet --version` ≥ 8).
- The `sek` tool is installed: `dotnet tool install -g sek`.
- A Spec Kit feature exists with a `spec.md` (and ideally acceptance criteria).

## Inputs

- The active feature directory (the current `specs/<feature>/` folder).
- `spec.md` — especially the **acceptance criteria** and any state/behavior rules.

## Steps

1. **Read the spec.** Load `spec.md` for the active feature. Extract the entities,
   the operations/actions the system performs, the preconditions/guards on those
   actions, and the acceptance criteria that define "done".

2. **Design the model state.** Identify the minimal state needed to decide when
   each action is enabled and what it changes. Represent it as public properties
   on a class deriving from `Sek.Modeling.ModelProgram`.

3. **Write rules.** For each action, write a `[Rule("Area.Action")]` method that:
   - guards its preconditions with `Require(condition, "reason")`;
   - mutates state to reflect the action's effect;
   - takes parameters whose domains come from Cord `Condition.In(...)` (value
     params) or from reachable model objects (reference params).

4. **Write accepting conditions.** Add `[AcceptingCondition]` `bool` methods that
   encode the acceptance criteria (the goal states).

5. **Author Cord.** Create a `Config.cord` with a base `config` that declares the
   actions and bounds (`StateBound`, `StepBound`), a derived `config` with the
   parameter domains (`Condition.In`, `Combination.Pairwise`, `Condition.IsTrue`),
   and a `machine` that does `construct model program from <Config>`.

6. **Scaffold the project.** Create a net8 SDK-style `Model.csproj` referencing the
   `Sek.Modeling` package, and a `.specexplorerkit/config.json` pointing at the
   built model assembly and the Cord directory. Use `sek init` as a starting point.

7. **Build and validate.** Run:
   ```bash
   dotnet build path/to/Model.csproj
   sek validate --project path/to/feature
   ```
   Fix any reported mismatches between Cord actions and model rules.

## Output

- `Model/Model.cs` — the `ModelProgram` with rules and accepting conditions.
- `Model/Config.cord` — configurations and at least one exploration machine.
- `.specexplorerkit/config.json` — the SEK project descriptor.

## Guidance

- Keep the model *minimal*: only the state needed to decide guards and acceptance.
- Prefer structural state (lists/records) so equal states de-duplicate.
- Map every acceptance criterion to either an accepting condition or a reachable
  target state, so `/speckit.sek.explore` can prove it is reachable.
- Do **not** put system-under-test code in the model; the model describes intended
  behavior only. Conformance to the real implementation is checked separately by
  `/speckit.sek.verify`.
