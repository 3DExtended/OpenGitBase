<!-- forge: #48 -->

# Hybrid `runs-on` routing

## Metadata

- ID: ci-15
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Route claimed jobs to the correct compute pool by **Hosting Profile**: `ogb-hosted` → platform nodes, `organization-self-hosted` → owning org's `OwnOrgOnly` nodes, `community-hosted` → any org's `CrossOrgAllowed` nodes. Support multiple hosting profiles in the same pipeline stage so jobs dispatch to different pools concurrently.

## Acceptance criteria

- [ ] `ClaimJob` returns only jobs whose `runs-on` matches the node's hosting scope and org eligibility
- [ ] `ogb-hosted` jobs never dispatch to org-only nodes
- [ ] `organization-self-hosted` jobs dispatch only to `OwnOrgOnly` nodes owned by the repository's org
- [ ] `community-hosted` jobs dispatch to `CrossOrgAllowed` nodes
- [ ] Single pipeline stage can run jobs on platform and org nodes in parallel
- [ ] No eligible node → job stays queued with observable reason
- [ ] Integration test: same-stage jobs with different `runs-on` land on correct node types

## Blocked by

- [10-first-ogb-hosted-job-tracer.md](./10-first-ogb-hosted-job-tracer.md) (ci-10)
- [06-org-compute-self-service-enrollment.md](./06-org-compute-self-service-enrollment.md) (ci-06)

## User stories covered

- 6 — Every job requires explicit `runs-on`.
- 7 — Hosting profile selection via `runs-on`.
- 10 — Different `runs-on` values within the same stage.
- 35–37 — Org enrollment and hosting scope (routing consumption).

## Notes

- Platform Kafka wake (ci-17) improves latency for `ogb-hosted` only.
- Resource limits may differ by profile; `ogb-hosted` defaults enforced in ci-18.
