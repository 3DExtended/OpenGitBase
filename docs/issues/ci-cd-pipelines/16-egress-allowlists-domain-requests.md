# Egress allowlists + domain requests

## Metadata

- ID: ci-16
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Enforce **Egress Policy** on the compute host (not inside the guest): `ogb-hosted` and `community-hosted` jobs use the platform allowlist; `organization-self-hosted` jobs use platform defaults ∪ org-specific allowances. Denied connections produce clear log messages. Authors submit **Domain Allowance Requests** with justification; platform admins approve platform/community egress; org owners approve org self-hosted egress.

## Acceptance criteria

- [ ] Host-level firewall or proxy enforces allowlists per hosting profile
- [ ] `ogb-hosted` / `community-hosted` use platform-managed domain list
- [ ] `organization-self-hosted` merges platform defaults with org allowlist
- [ ] Denied egress events appear in job logs with actionable message
- [ ] Author can submit domain allowance request with justification from pipeline failure context
- [ ] Platform admin can approve/reject platform-scoped requests
- [ ] Org owner can approve/reject org-scoped requests
- [ ] Integration test: blocked domain fails with log hint; approved domain succeeds

## Blocked by

- [10-first-ogb-hosted-job-tracer.md](./10-first-ogb-hosted-job-tracer.md) (ci-10)
- [06-org-compute-self-service-enrollment.md](./06-org-compute-self-service-enrollment.md) (ci-06)

## User stories covered

- 43 — Org owner approves **Domain Allowance Requests**.
- 52 — Platform admin approves platform requests.
- 55 — Egress enforced on the host.
- 56 — Restricted default egress with allowlists.
- 61 — Request new egress domain with justification.
- 62 — Clear log messages when egress is denied.

## Notes

- Guest cannot disable host enforcement (story 55).
- Package registries needed for common base images should ship in platform defaults.
