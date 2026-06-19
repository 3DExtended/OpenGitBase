# git-https-02 — implementation record

## Status

- Branch: `feat/git-https-pat`
- Base branch: `main`
- Tests: passing (access-check PAT tests + storage node registration)
- Completion: **done** (issue 02 scope)

## Summary

Extended repository access-check to accept PAT or SSH public key (mutually exclusive). PAT path validates via `ValidateGitAccessTokenQuery`, enforces read/write scope before storage routing, and returns `storageNodeInternalGitHttpPort`.

- `StorageNode.InternalGitHttpPort` (default 8082) on entity, DTO, registration query
- Migration `AddStorageNodeInternalGitHttpPort`
- `storage-agent.sh` reports `internalGitHttpPort` on registration
- API/Dispatcher access-check response includes git HTTP port

## Linked Context

- PRD: `docs/prd/git-https-personal-access-tokens.md`
- Work item: `docs/issues/git-https-pat/02-pat-repository-access-check.md`

## Dependency Graph

### Direct dependencies (blocked by)

- git-https-01

### Full chain

`git-https-01` → `git-https-02`

## Tests

- `RepositoryAccessChecksControllerTests` — PAT paths, scope denial, routing port
- `RegisterStorageNodeQueryHandlerTests` — default git HTTP port persisted

## Notes

- Heartbeat does not yet re-report git HTTP port (registration + re-registration updates it)
