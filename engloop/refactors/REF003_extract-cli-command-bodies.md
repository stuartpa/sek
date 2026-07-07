# REF003: Extract CLI command bodies from top-level `Program.cs` into a testable `Sek.Cli` type

- **Date:** 2026-07-07
- **Cadence:** targeted refactor (unblocks `sek` ≥95% branch; endorsed by ARC001)
- **Status:** CHOSEN (implementing)
- **Emitted SEED:** none (implemented in-cycle)

## Signals gathered

| Signal | Finding |
|---|---|
| Architecture drift / boundary violations | ARC001 wants semantic/back-end logic **not** in the CLI; today `Program.cs` (1249 lines of top-level statements) holds the entire explore/interpret/construct/slice/point-shoot engine-driving logic as local functions. |
| Coverage / test health | `sek` is stuck at **75% branch** — the deep `Interpret`/construct/slice branches and per-command error paths in `Program.cs` are unreachable via unit tests because top-level local functions aren't callable; driving them all via CLI invocation needs a contrived sample per construct×option×error. |
| Hot spots | `Program.cs` is the largest, most-branchy file and the last module far from the readiness bar. |

## Decision-tree branch taken

**Branch 2 — architecture drift / testability boundary.** The CLI's command logic must be a
callable, testable surface (ARC001), not top-level local functions. This is the keystone that lets
`sek` reach ≥95% branch.

## Chosen refactor

Wrap the top-level `Program.cs` into a library type `Sek.Cli.SekCli`:
- `public static int Run(string[] args)` holds the argument dispatch (the former top-level body).
- Every former local function (command handlers `CmdExplore`/`CmdTest`/…, and the engine-driving
  helpers `ExploreMachine`/`Interpret`/`InterpretConstruct`/`InterpretTarget`/slice+point-shoot
  helpers) becomes a **`public static` method** on `SekCli`, so unit tests can call the branchy logic
  directly against sample projects.
- `Program.cs` top-level collapses to a one-line entry: `return Sek.Cli.SekCli.Run(args);`.
- **Behavior preserved exactly** — the 61-sample exploration regression and the 387-test suite must
  stay green (the refactor is mechanical; no logic change).

## Expected long-term benefit

The CLI's command + interpret logic becomes a first-class, unit-testable API (aligning with ARC001's
"back end is pure w.r.t. the IR / semantic decisions don't live in the CLI"), which both raises `sek`
branch coverage past the readiness bar and makes future CLI features testable in isolation.

## Hand-off

Implemented directly (guarded by the 61-sample regression + 387 tests). Follow-up: add unit tests
against the now-public `SekCli` methods to drive `sek` branch ≥95%.
