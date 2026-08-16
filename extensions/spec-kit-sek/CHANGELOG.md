# Changelog

All notable changes to the SpecExplorerKit Spec Kit extension are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.2] - 2026-08-16

### Added

- SEK-owned `sek-cord-authoring` and `using-sek-to-generate-tests` Agent Skills,
  including parse-validated Cord assets and an explicit support matrix.
- Public implementation-accurate Cord reference, support matrix, and advanced authoring patterns.
- `SpecExplorerKit.Modeling` is packed beside `SpecExplorerKit.Tool`, so downstream model
  projects no longer require a sibling SEK source checkout.
- Generated xUnit projects snapshot their binding and built dependencies under
  `BindingAssets`, removing absolute paths and the ambient `SEK_BINDING` fallback.

### Fixed

- Changed the extension ID to `sek` so it matches the existing `/speckit.sek.*`
  command namespace and installs successfully with current Spec Kit. The release asset remains
  `spec-kit-sek.zip`.
- Updated generated-test documentation for portable `BindingAssets` snapshots and removed the
  obsolete `SEK_BINDING` fallback guidance.

## [0.1.1] - 2026-07-04

### Changed

- Aligned with SpecExplorerKit v0.1.1, whose `sek` tool bundles a Linux `libz3.so`
  so Z3-backed exploration and conformance work on Windows, macOS, and Linux.

## [0.1.0] - 2026-07-04

### Added

- Initial release of the SpecExplorerKit Spec Kit community extension.
- `/speckit.sek.model` — generate a SEK model program and Cord scenarios from a feature spec.
- `/speckit.sek.explore` — explore a model into a `.seexpl` transition system and summarize coverage.
- `/speckit.sek.verify` — replay an exploration against the implementation and report conformance.
- Extension manifest (`extension.yml`), documentation, and MIT license.
