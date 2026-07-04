---
name: view-seexpl
description: >
  View and explain SpecExplorerKit exploration graphs (.seexpl files) directly in VS
  Code without Visual Studio. Use when the user asks to view, open, render, visualize,
  or explain a .seexpl exploration, or an "exploration graph" / "state machine" produced
  by the `sek` tool. Renders the graph as a Mermaid state diagram in chat and can also
  produce a standalone HTML view.
---

# Viewing SpecExplorerKit explorations (.seexpl) in VS Code

`.seexpl` files are SpecExplorerKit's exploration graphs: a JSON transition system
(states + transitions + accepting flags) emitted by `sek explore`. They replace the
classic Spec Explorer binary format and are designed to be viewed without Visual Studio.

## How to render one

Prefer the `sek` tool (it already knows the schema):

```
sek view <path-to.seexpl> --format mermaid    # Mermaid state diagram (great for chat)
sek view <path-to.seexpl> --format dot        # Graphviz DOT
sek view <path-to.seexpl> --format html --out <path.html>   # self-contained HTML
```

`sek` is the SpecExplorerKit CLI (see the `SEK` folder). If it is not on PATH, run it
via the project: `dotnet run --project SEK/src/Sek.Cli -- view <file>`.

### To show a graph in chat
1. Run `sek view <file> --format mermaid`.
2. Put the output inside a ```mermaid fenced block so it renders inline.
3. If the graph is large (hundreds of states), summarize instead: report the state /
   transition / accepting counts (from the JSON `metadata`) and render only a relevant
   sub-path (e.g. an accepting path) rather than the whole graph.

### To open a graph as a page
1. Run `sek view <file> --format html --out out.html`.
2. Open `out.html` with the VS Code Simple Browser (command: "Simple Browser: Show") or
   a Live Preview extension.

## Reading a .seexpl directly (no tool)
The JSON has `machine`, `initialState`, `states[]` (`id`, `accepting`, `initial`) and
`transitions[]` (`from`, `to`, `action`, `arguments`). To hand-render Mermaid:

```
stateDiagram-v2
  [*] --> <initialState>
  <from> --> <to> : <action>(<arguments>)   // one line per transition
  <acceptingStateId> --> [*]                 // for each accepting state
```

## Related `sek` commands
- `sek explore <machine>` — produce a `.seexpl` from the model + Cord config.
- `sek test <machine>` — explore then replay against the SUT (conformance).
- `sek validate` — check the model program and Cord scripts line up.
