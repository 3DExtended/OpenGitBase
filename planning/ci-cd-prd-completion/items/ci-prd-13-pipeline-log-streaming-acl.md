# Pipeline log visibility + live streaming UI

## Metadata

- ID: ci-prd-13
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-19)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Complete pipeline **visibility and log UX** per PRD: enforce public vs private log access on the API, improve run detail page with live log delivery (SSE or efficient poll), and render structured log sections (layer, install, workspace, script) as distinct UI regions.

## Acceptance criteria

- [ ] Public repository: unauthenticated users can read pipeline logs via API and UI
- [ ] Private repository: non-members denied; members can read
- [ ] Run detail page updates logs during running jobs without full page refresh
- [ ] UI groups log lines by `section` field from executor
- [ ] Cancel button remains available to users with repository write access on running jobs
- [ ] Tests cover ACL matrix (public anon, private member, private outsider)

## Blocked by

- None — can start immediately

## User stories covered

- 26 — Job logs streamed during execution
- 27 — Structured log sections
- 32 — Public repo logs world-readable
- 33 — Private repo logs members-only

## Notes

- ci-19 shipped list/detail/poll/cancel/badge; this slice closes ACL and streaming gaps.
- API may need SSE endpoint or WebSocket; polling interval optimization acceptable if streaming is deferred with documented trade-off.
