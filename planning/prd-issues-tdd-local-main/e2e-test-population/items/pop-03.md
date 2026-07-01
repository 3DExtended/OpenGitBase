## Summary

Added `GitTestDataLayout` and `GitTestDataFixture` to seed a known file tree (README, nested path, SVG, >1MB blob, anchor doc) via PAT push. Refactored `BrowseE2eTests` and added `KnownFileTreeIncludesNestedSvgAndLargeBlob`. Extended `GitOperations.CommitPathsAsync`.

## Linked Context

- PRD: `docs/prd/e2e-test-population.md`
- Work item: `pop-03`

## Dependency Graph

### Direct dependencies (blocked by)

- pop-02

### Full chain

`pop-02 -> pop-03`

## Status

- Branch: `main`
- Tests: `dotnet test tests/OpenGitBase.E2E.Tests --filter Category=E2EUnit` — 12 passed; compose browse tests pending stack
- Visual snapshots: none
- Commit(s): `1caf1b6`
