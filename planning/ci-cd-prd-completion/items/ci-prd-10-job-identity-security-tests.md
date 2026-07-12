# Job Identity security contract tests

## Metadata

- ID: ci-prd-10
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-07)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Add **contract tests** proving the Job Identity and Compute Node Identity separation described in the PRD: node enrollment credentials cannot read arbitrary repositories; per-job credentials are scoped to one commit SHA, expire at teardown, and are insufficient for cross-repo access.

## Acceptance criteria

- [ ] Test: compute node heartbeat/register identity cannot fetch unrelated repo content
- [ ] Test: job identity grants read only for job's repository and `afterSha`
- [ ] Test: revoked/expired job identity rejected by git/workspace materialization path
- [ ] Test: job identity cannot be reused across jobs or SHAs
- [ ] Documented security boundary table aligned with PRD stories 57–60

## Blocked by

- None — can start immediately

## User stories covered

- 57 — Per-job credentials for repo access
- 58 — Job credentials scoped to one SHA
- 59 — Job credentials expire at teardown
- 60 — Node credentials cannot read arbitrary repos

## Notes

- Prefer contract tests at Job Identity Service boundary over full Firecracker integration.
- Can run in parallel with UI and sandbox slices.
