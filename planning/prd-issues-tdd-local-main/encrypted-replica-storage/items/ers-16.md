## Summary

Added `MaxBytesOverride` on repositories with eligibility rules (org-owned, >3 healthy org nodes, all replicas on org nodes, full RF=4 replica set). API endpoints for eligibility and PATCH override; push/pre-receive and usage reporting enforce the override; placement uses override for `RequiredBytesPerNode` when set. Repo settings UI with `RepositoryByteOverridePanel` and Playwright visual snapshots.

## Linked Context

- PRD: `docs/prd/encrypted-replica-storage.md`
- Work item: `ers-16`

## Dependency Graph

### Direct dependencies (blocked by)

- ers-15

### Full chain

`ers-13/14 -> ers-15 -> ers-16`

## Status

- Branch: `main`
- Tests: passing
  - `dotnet test tests/OpenGitBase.Api.Tests --filter FullyQualifiedName~RepositoryByteOverrideServiceTests|FullyQualifiedName~CheckRepositoryAccess_WhenWriteExceedsMaxBytesOverride`
  - `dotnet test tests/OpenGitBase.Features.Repository.Tests --filter FullyQualifiedName~GetRepositoryUsageQueryHandlerTests`
  - `pnpm test:visual -- tests/visual/shell.spec.ts -g "repository byte override"`
- Visual snapshots: `applications/opengitbase-web/tests/visual/shell.spec.ts-snapshots/repo-byte-override-*.png`
- Commit(s): pending
