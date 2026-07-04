# SpecExplorerKit Documentation

This folder contains the documentation source for SpecExplorerKit (SEK), built with
[DocFX](https://dotnet.github.io/docfx/) — the same toolchain used by
[Spec Kit](https://github.com/github/spec-kit/tree/main/docs).

## Build locally

```bash
dotnet tool install -g docfx
cd docs
docfx docfx.json --serve
# open http://localhost:8080
```

To build without serving:

```bash
docfx docs/docfx.json
# output in docs/_site
```

## Structure

- `docfx.json` — DocFX configuration.
- `index.md` — landing page.
- `toc.yml` — top navigation.
- `install/` — installation.
- `guides/` — task-oriented walkthroughs (quickstart, authoring, Cord, conformance, migration).
- `concepts/` — the ideas (MBT, model programs, exploration, Cord, Z3, object domains, conformance).
- `reference/` — CLI, Cord grammar, project config, `.seexpl` format.
- `samples/` — the ported Spec Explorer 2010 sample suite.
- `community/` — using SEK as a Spec Kit extension.
- `release-notes/` — per-version release notes.

## Deployment

The `docs` workflow (`.github/workflows/docs.yml`) builds this site and publishes
it to GitHub Pages on pushes to `main` that touch `docs/`.
