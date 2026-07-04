---
title: Install SEK
description: Install the SpecExplorerKit (sek) command-line tool and prerequisites.
---

# Install SEK

SEK is a .NET global tool named `sek`. It runs on Windows, macOS, and Linux.

## Prerequisites

- **.NET SDK 8.0 or later** — check with `dotnet --version`.
  Download from <https://dotnet.microsoft.com/download>.
- No Visual Studio, no Windows-only runtime, no legacy `Microsoft.Modeling`.
- The Z3 native library ships with the `Microsoft.Z3` NuGet package that SEK
  depends on — nothing extra to install.

## Install the `sek` tool

### From a release package

Download `SpecExplorerKit.Tool.<version>.nupkg` from the
[latest release](https://github.com/stuartpa/sek/releases/latest) and
install it from the folder that contains it:

```bash
dotnet tool install -g SpecExplorerKit.Tool --add-source ./path/to/folder
```

Verify:

```bash
sek version
sek z3        # self-test the Z3 backend (prints SATISFIABLE)
```

### From source

```bash
git clone https://github.com/stuartpa/sek
cd sek
dotnet build src/Sek.Cli/Sek.Cli.csproj
# invoke the freshly built tool:
dotnet src/Sek.Cli/bin/Debug/sek.dll version
```

## Install the Spec Kit extension

If you use [Spec Kit](https://github.github.io/spec-kit/), add the SEK extension:

```bash
specify extension add spec-kit-sek \
  --from https://github.com/stuartpa/sek/releases/latest/download/spec-kit-sek.zip
```

See [Using SEK as a Spec Kit extension](../community/spec-kit-extension.md).

## Editor integration (VS Code)

SEK is CLI-first and needs no proprietary extension. For a better experience:

- Add tasks that call `sek explore` / `sek view`.
- Use the bundled **view-seexpl** skill to render `.seexpl` graphs inline.

## Next steps

- [Quickstart](../guides/quickstart.md)
- [Concepts](../concepts/index.md)
- [CLI reference](../reference/cli.md)
