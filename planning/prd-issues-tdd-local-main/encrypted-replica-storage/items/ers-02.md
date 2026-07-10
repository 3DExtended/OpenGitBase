# ers-02 — RF=4 schema, repository keys, and artifact library

## Summary

Added RF=4 schema extensions (replica roles, replication states, RepositoryKey table), RepositoryKeyService, EncryptedArtifactService, and unit tests.

## Linked Context

- PRD: `docs/prd/encrypted-replica-storage.md`
- Work item: `ers-02`

## Dependency Graph

### Direct dependencies (blocked by)

- None

### Full chain

`ers-02`

## Status

- Branch: `main`
- Tests:
  - `dotnet test tests/OpenGitBase.Common.Tests --filter EncryptedArtifactServiceTests|RepositoryKeyProtectionServiceTests` — 5 passed
  - `dotnet test tests/OpenGitBase.Api.Tests --filter RepositoryKeyServiceTests` — 3 passed
- Visual snapshots: none
- Commit(s): pending
