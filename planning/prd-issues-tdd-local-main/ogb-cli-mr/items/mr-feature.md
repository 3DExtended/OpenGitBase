## Summary

Shipped full `ogb mr` command group: list, view, status, diff, create, close, ready, edit, approve, merge. Extended `IOgbApiClient`, output writers, `IGitBranchResolver`, and CLI routing. Unit + integration tests green; compose E2E test and smoke script added.

## Linked Context

- PRD: `docs/prd/ogb-cli-mr.md`
- Work items: mr-01 … mr-12

## Dependency Graph

All items on `main` in one implementation pass.

## Status

- Branch: `main`
- Tests: `dotnet test tests/OpenGitBase.Cli.Tests` (74), `dotnet test tests/OpenGitBase.Cli.Integration.Tests` (3)
- Visual snapshots: none (CLI-only)
- Compose E2E: test added; not run (Docker daemon down)
- Commit(s): pending
