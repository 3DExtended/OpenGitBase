# Node Identity + agent API authentication

## Metadata

- ID: ci-rt-01
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Issue a long-lived **Node Identity** bearer token when a **Compute Node** successfully registers. Require that token on all agent-facing control-plane endpoints: heartbeat, claim, status update, dependency outcome recording, and incremental log append.

The **Compute Node Agent** stores the token after registration and sends `Authorization: Bearer` on every outbound API call. Unauthenticated or invalid tokens receive 401. **Node Identity** must not authorize repository or workspace access.

Update heartbeat to report accurate `RunningJobs` counts while jobs are in flight.

## Acceptance criteria

- [ ] Registration returns a **Node Identity** token; token hash stored server-side
- [ ] Agent claim, heartbeat, status, and dependency-outcome endpoints reject missing or invalid **Node Identity**
- [ ] Contract test: **Node Identity** cannot download workspace archives (reserved for **Job Identity** in ci-rt-02)
- [ ] Heartbeat reports non-zero `RunningJobs` while executing a job
- [ ] Existing compose agent flow works after bootstrap enrollment with new token handling

## Blocked by

- None — can start immediately

## User stories covered

- 12 — **Node Identity** unable to read repository contents
- 16 — Org **Compute Node Agent** authenticates outbound API calls
- 19 — Accurate running-job counts in heartbeats

## Notes

- Bearer token sufficient for v1; mTLS is out of scope per runtime PRD.
- Process sandbox execution path remains valid for compose while auth is added.
