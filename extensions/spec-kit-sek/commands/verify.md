# `/speckit.sek.verify`

Replay the feature's explored transition system against the **real
implementation** (the system under test) and report **conformance** — whether the
implementation behaves as the model says it should.

## Prerequisites

- A SEK model + Cord exist and explore successfully (see `/speckit.sek.explore`).
- A **binding** exists that maps model actions to implementation calls (an adapter
  assembly + namespace), declared in `.specexplorerkit/config.json`.
- The `sek` tool is installed: `dotnet tool install -g sek`.

## Steps

1. **Ensure a binding is configured.** In `.specexplorerkit/config.json`:
   ```json
   {
     "model":   { "assembly": "Model/bin/Debug/Model.dll", "type": "Ns.Model" },
     "cord":    "Model",
     "binding": { "assembly": "Adapter/bin/Debug/Adapter.dll", "namespace": "Adapter" },
     "out":     ".specexplorerkit/out"
   }
   ```
   The binding namespace should expose one method per model action label.

2. **Build** the model and the adapter/implementation:
   ```bash
   dotnet build path/to/Model.csproj
   dotnet build path/to/Adapter.csproj
   ```

3. **Run conformance:**
   ```bash
   sek test <MachineName> --project path/to/feature
   ```
   SEK explores the machine, then replays every transition against the binding and
   reports how many succeeded/failed and which actions were covered.

4. **Report.** Summarize: transitions replayed, succeeded, failed, actions covered,
   and the overall `TEST PASSED` / `TEST FAILED` verdict. On failure, include the
   first few failing transitions and the divergence between expected and observed
   behavior.

## Output

- A conformance report (pass/fail, counts, covered actions, first failures).
- A recommended next action: fix the implementation, or refine the model if the
  spec's intended behavior changed.

## Guidance

- A conformance failure is either an implementation bug **or** a model/spec that no
  longer matches intended behavior — decide which, and update the losing side.
- Keep the binding thin: it should translate a model action + arguments into a
  single call on the implementation and (optionally) check the returned value.
- Treat `/speckit.sek.verify` as a gate before marking a feature's tasks complete:
  full transition coverage with zero failures is strong evidence the implementation
  conforms to the specified behavior.
