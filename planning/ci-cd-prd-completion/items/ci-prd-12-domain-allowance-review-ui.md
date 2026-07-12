# Admin + org domain allowance review UI

## Metadata

- ID: ci-prd-12
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-16)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Web UI for **Domain Allowance Request** workflows: pipeline authors submit requests (may already be API-only); platform admins approve/deny platform and community egress; org owners approve org-scoped requests for `organization-self-hosted` effective allowlists.

## Acceptance criteria

- [ ] Authors can submit domain allowance request with domain, justification, and scope from UI
- [ ] Admin page lists pending platform-scoped requests with approve/deny actions
- [ ] Org settings page lists pending org-scoped requests; org owners can approve/deny
- [ ] Approved domains appear in effective egress allowlist for correct scope
- [ ] Denied requests show status to requester; no silent failure
- [ ] API authorization: only platform admin for platform requests; only org owner for org requests

## Blocked by

- [ci-prd-02-org-compute-settings-ui.md](./ci-prd-02-org-compute-settings-ui.md)

## User stories covered

- 43 — Org owner approves **Domain Allowance Requests** for org self-hosted
- 52 — Platform admin approves platform domain requests
- 61 — Authors request new egress domain with justification

## Notes

- Approve/deny API routes exist on `PipelineController`; this slice is UI + auth polish.
- May live under org compute settings or separate `/{owner}/ci` egress section.
