---
title: CLI reference (sek)
description: Complete reference for the sek command-line tool — init, validate, explore, view, test, z3, version.
---

# CLI reference: `sek`

`sek` is the SpecExplorerKit command-line tool. Run `sek` with no arguments (or
`sek --help`) for usage.

```text
sek <command> [options]
```

| Command | Purpose |
|---|---|
| [`init`](#sek-init) | Scaffold a `.specexplorerkit/` project |
| [`validate`](#sek-validate) | Check the model and Cord line up |
| [`explore`](#sek-explore) | Explore a machine into a `.seexpl` graph |
| [`view`](#sek-view) | Render a `.seexpl` graph |
| [`test`](#sek-test) | Explore, then replay against the SUT (conformance) |
| [`z3`](#sek-z3) | Self-test the Z3 backend |
| [`version`](#sek-version) | Print the version |

Most commands accept `--project <dir>` to point at the project directory (the folder
containing `.specexplorerkit/config.json`); it defaults to the current directory.

---

## `sek init`

Scaffold a new project.

```bash
sek init [--project <dir>]
```

Creates `.specexplorerkit/config.json` (and an `out/` directory) with a starter
configuration you then edit to point at your model, Cord, and (optionally) binding.

---

## `sek validate`

Static checks that the model and Cord agree.

```bash
sek validate [--project <dir>]
```

Reports:

- the resolved model type, its rules, and accepting-condition count;
- the number of Cord configs and machines;
- any Cord **action with no matching model rule**;
- any machine that **`construct`s from an unknown** config/machine.

Exit code is non-zero if problems are found.

---

## `sek explore`

Explore a machine into a transition system.

```bash
sek explore <machine> [--project <dir>] [--solver z3|enum] [--out <file>]
```

| Option | Default | Meaning |
|---|---|---|
| `<machine>` | — | the Cord machine to explore (required) |
| `--project <dir>` | current dir | project directory |
| `--solver z3\|enum` | `z3` | parameter solver ([Z3](../concepts/parameter-generation.md) or enumerative) |
| `--out <file>` | `.specexplorerkit/out/<machine>.seexpl` | output path |

Prints a summary (states, transitions, accepting states, and `(bound hit)` if a
bound stopped the search). If the machine has no model program (pure Cord behavior),
SEK explores it in [behavior mode](../concepts/state-exploration.md#behavior-mode).

---

## `sek view`

Render a `.seexpl` graph.

```bash
sek view <file.seexpl> [--format mermaid|dot|html] [--out <file>]
```

| Option | Default | Meaning |
|---|---|---|
| `<file.seexpl>` | — | the graph to render (required) |
| `--format` | `mermaid` | `mermaid`, `dot`, or `html` |
| `--out <file>` | stdout | write to a file instead of stdout |

---

## `sek test`

Explore a machine, then replay every transition against the configured binding and
report conformance.

```bash
sek test <machine> [--project <dir>] [--solver z3|enum]
```

Requires a `binding` block in the project config. Prints transitions replayed,
succeeded, failed, actions covered, and a `TEST PASSED` / `TEST FAILED` verdict.
Exit code is non-zero on failure. `sek run` is an alias.

---

## `sek z3`

Self-test the Z3 backend; prints `SATISFIABLE` and a sample model if the native Z3
library loaded correctly.

```bash
sek z3
```

---

## `sek version`

Print the tool version.

```bash
sek version
```

---

## Running the in-repo build

If you haven't installed the global tool, invoke the freshly built assembly:

```bash
dotnet src/Sek.Cli/bin/Debug/sek.dll <command> [options]
```
