# SpecExplorerKit — Spec Kit extension

**Model-based testing for spec-driven development.** This
[Spec Kit](https://github.github.io/spec-kit/) community extension brings
[SpecExplorerKit (SEK)](https://github.com/stuartpa/sek) into the SDD
lifecycle: turn a feature's acceptance criteria into an executable **model**,
**explore** it into a finite-state transition system (Z3-powered), and **verify**
that your implementation conforms.

## Features

- `/speckit.sek.model` — generate a SEK model program + Cord scenarios from `spec.md`.
- `/speckit.sek.explore` — explore the model into a `.seexpl` transition system and summarize coverage.
- `/speckit.sek.verify` — replay the exploration against the implementation and report conformance.

## Installation

```bash
# Install the SEK tool (dependency)
dotnet tool install -g sek

# Add the extension to your Spec Kit project
specify extension add spec-kit-sek --from https://github.com/stuartpa/sek/releases/latest/download/spec-kit-sek.zip
```

Or, for local development against a checkout:

```bash
specify extension add --dev ./extensions/spec-kit-sek
```

## Requirements

| Dependency | Version | Purpose |
|---|---|---|
| Spec Kit | `>= 0.1.0` | host toolkit |
| .NET SDK | `>= 8.0` | runs SEK |
| `sek` tool | latest | model exploration & conformance |

## Usage

```text
/speckit.sek.model      # after /speckit.specify — build a model from the spec
/speckit.sek.explore    # explore the model; review states/transitions/coverage
/speckit.sek.verify     # replay against the implementation; gate on conformance
```

See each command's documentation under [`commands/`](commands/), and the full
SEK documentation at <https://github.com/stuartpa/sek>.

## Configuration

The commands operate on a `.specexplorerkit/config.json` in the feature folder:

```json
{
  "model":   { "assembly": "Model/bin/Debug/Model.dll", "type": "Ns.Model" },
  "cord":    "Model",
  "binding": { "assembly": "Adapter/bin/Debug/Adapter.dll", "namespace": "Adapter" },
  "out":     ".specexplorerkit/out"
}
```

## Troubleshooting

- **`sek: command not found`** — run `dotnet tool install -g sek` and ensure the
  .NET global tools directory is on your `PATH`.
- **`machine '<name>' not found`** — check the machine name matches a `machine`
  declared in your Cord script (`sek validate` lists them).
- **Exploration hits a bound** — the modeled behavior is unbounded; tighten the
  Cord scenario rather than raising `StateBound`/`StepBound`.

## Contributing

Issues and pull requests are welcome at
<https://github.com/stuartpa/sek>. This extension is released under the
[MIT License](LICENSE).
