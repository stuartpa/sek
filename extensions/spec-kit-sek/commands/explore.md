# `/speckit.sek.explore`

Explore the feature's **SpecExplorerKit** model into a finite-state transition
system and summarize what the model can do, so you can review behavior coverage
and reachable states before generating or running tests.

## Prerequisites

- A SEK model + Cord exist for the feature (see `/speckit.sek.model`).
- The `sek` tool is installed: `dotnet tool install -g sek`.

## Steps

1. **Build the model** so the latest rules are compiled:
   ```bash
   dotnet build path/to/Model.csproj
   ```

2. **Explore** each machine of interest. The default solver is Z3:
   ```bash
   sek explore <MachineName> --project path/to/feature
   ```
   Use `--solver enum` to cross-check with the dependency-free enumerative solver,
   or `--out <file>.seexpl` to control the output path.

3. **Render** the transition system for review:
   ```bash
   sek view path/to/.specexplorerkit/out/<MachineName>.seexpl --format html --out report.html
   ```
   Mermaid (`--format mermaid`) is ideal for inlining into `plan.md` or a PR.

4. **Summarize.** Report the number of states, transitions, and accepting states;
   list the actions that were covered; and call out any `bound hit` (the graph was
   truncated by `StateBound`/`StepBound`, which usually means the modeled behavior
   is unbounded and needs a tighter scenario).

## Output

- One `.seexpl` transition system per explored machine under `.specexplorerkit/out/`.
- A rendered `report.html` / Mermaid diagram for review.
- A short coverage summary suitable for pasting into `plan.md` or a pull request.

## Guidance

- If exploration hits a bound, tighten the Cord scenario (add sequencing, choice,
  or repetition operators) or reduce parameter domains rather than raising bounds
  blindly.
- Compare the Z3 and enumerative solvers on small models — they should agree; a
  discrepancy indicates a constraint that Z3 models differently than expected.
- Reachability of an accepting state is evidence that an acceptance criterion is
  satisfiable by the model; unreachable accepting states point to over-constrained
  guards.
