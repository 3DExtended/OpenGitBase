## Summary

Extended `RunOptions` with `--tag` and `--feature` CLI flags; runner combines them with tier filters via `BuildTestFilter`. Documented smoke and feature iteration commands in E2E README.

## Linked Context

- PRD: `docs/prd/e2e-test-population.md`
- Work item: `pop-05`

## Dependency Graph

### Direct dependencies (blocked by)

- None

### Full chain

`pop-05`

## Status

- Branch: `main`
- Tests: `dotnet test tests/OpenGitBase.E2E.Tests --filter Category=E2EUnit` — 11 passed
- Visual snapshots: none
- Commit(s): `aac93f0`
