# SSE log streaming + incremental agent posts

## Metadata

- ID: ci-rt-12
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Forward log lines from the agent to the control plane incrementally as they are produced (not only after phase completion). Add an SSE tail endpoint per **Job** for authorized viewers. Update **Pipelines UI** run detail to consume SSE instead of polling-only logs while preserving section grouping (`layer`, `install`, `workspace`, `script`).

Add Playwright visual snapshot coverage for the streaming log presentation if UI changes affect appearance.

## Acceptance criteria

- [ ] Agent appends log lines during execution via authenticated incremental API
- [ ] SSE endpoint streams new lines to clients with repository read authorization
- [ ] Pipelines run detail shows logs updating during running jobs without full-page poll
- [ ] Structured log sections preserved in storage and UI grouping
- [ ] Public/private log ACL unchanged (stories 47–48)

## Blocked by

- [ci-rt-01-node-identity-agent-auth.md](./ci-rt-01-node-identity-agent-auth.md) (ci-rt-01)

## User stories covered

- 36 — Job logs streamed during execution
- 37 — Structured log sections preserved
- 47 — Public repo logs world-readable
- 48 — Private repo logs member-restricted

## Notes

- Vsock-to-host streaming originates in ci-rt-08; this slice covers API + UI delivery.
- Can be developed against process sandbox incremental posts before Firecracker lands.
