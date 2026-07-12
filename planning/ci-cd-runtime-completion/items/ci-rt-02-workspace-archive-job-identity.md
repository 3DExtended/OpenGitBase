# Workspace archive API + Job Identity fetch

## Metadata

- ID: ci-rt-02
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Add a control-plane **Workspace Materialization** endpoint that returns a commit-scoped repository archive for one **Job**, authenticated only by **Job Identity** (bearer token minted at claim). Support `GIT_DEPTH: 0` as a full worktree archive and `GIT_DEPTH: N` as a shallow archive.

Harden **Job Identity** validation for production use (no full-table scan). Extend contract tests: valid token scoped to one repository and SHA; cross-repo and cross-SHA rejected; revoked and expired tokens rejected.

Update the **Compute Node Agent** to fetch the workspace archive with **Job Identity** after claim and extract it on the host (guest mount deferred to ci-rt-07). Remove reliance on host bare-repo `PhysicalPath` for workspace materialization.

## Acceptance criteria

- [ ] Workspace archive endpoint requires **Job Identity**; **Node Identity** is rejected
- [ ] `GIT_DEPTH: 0` returns full worktree archive; `GIT_DEPTH: N` returns shallow archive
- [ ] Contract tests cover scope enforcement, expiry, and revocation
- [ ] Agent uses archive API instead of cloning `CI_REPOSITORY_GIT_DIR` on the host
- [ ] No job or node credentials appear in guest environment variables

## Blocked by

- [ci-rt-01-node-identity-agent-auth.md](./ci-rt-01-node-identity-agent-auth.md) (ci-rt-01)

## User stories covered

- 10 — Workspace fetched via **Job Identity** scoped to one SHA
- 11 — Job credentials expire and revoke at teardown
- 13 — `GIT_DEPTH: 0` full worktree materialization
- 14 — `GIT_DEPTH: N` shallow archive
- 15 — Credentials never injected into guest environment

## Notes

- Guest virtio-fs mount is ci-rt-07; this slice proves API + host-side extract on process sandbox if needed.
