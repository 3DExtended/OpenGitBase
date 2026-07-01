# Integration test promotion indexer

## Metadata

- ID: pop-07
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Build a **promotion indexer** that scans handler/API integration tests and outputs E2E candidacy list.

1. Scan `OpenGitBase.Features.*.Tests` and `OpenGitBase.Api.Tests` for test methods matching patterns: `Unauthorized`, `Forbidden`, `NotFound`, `HappyPath`, cross-boundary markers.
2. Output markdown or JSON list with: source test, suggested feature domain, suggested catalog status `pending`.
3. Optional: emit `Discovered/` skeleton stubs (reuse existing `TestGenerator` pattern) — human promotes to real scenarios.
4. Document run command in E2E README (manual/CI-optional).

Verifiable: indexer runs locally and produces ≥20 promotion candidates with feature mapping.

## Acceptance criteria

- [ ] Indexer script or dotnet tool produces candidate list
- [ ] Output format documented and storable alongside scenario catalog
- [ ] Does not auto-commit baselines
- [ ] At least one candidate manually promoted to demonstrate workflow

## Blocked by

- [01-scenario-catalog-authoring-checklist.md](./01-scenario-catalog-authoring-checklist.md)

## User stories covered

- 16, 17, 18, 19

## Notes

- Tooling slice — not required for manual scenario authoring but accelerates Wave 3.
