# Virtio-fs workspace in guest

## Metadata

- ID: ci-rt-07
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

After the host fetches the workspace archive via **Job Identity** (ci-rt-02), extract to a temp directory and share it into the Firecracker guest at `$CI_PROJECT_DIR` using virtio-fs or equivalent 9p share. Verify guest processes can read repository files at the expected path.

Teardown unmounts and deletes workspace temp storage without mutating immutable base or dependency layers.

## Acceptance criteria

- [ ] Agent no longer clones host bare-repo paths for workspace materialization
- [ ] Workspace archive extracted on host and mounted/shared into guest at `$CI_PROJECT_DIR`
- [ ] Guest tracer job can list or read a file from the materialized repository
- [ ] Workspace teardown on job completion does not leave stale mounts or temp dirs
- [ ] Works with both full and shallow archives per `GIT_DEPTH`

## Blocked by

- [ci-rt-02-workspace-archive-job-identity.md](./ci-rt-02-workspace-archive-job-identity.md) (ci-rt-02)
- [ci-rt-06-firecracker-launcher.md](./ci-rt-06-firecracker-launcher.md) (ci-rt-06)

## User stories covered

- 10 — Workspace scoped via **Job Identity**
- 13 — Full worktree available in guest at `$CI_PROJECT_DIR`
- 14 — Shallow archive materialization in guest

## Notes

- Credentials remain on host; share is workspace content only.
