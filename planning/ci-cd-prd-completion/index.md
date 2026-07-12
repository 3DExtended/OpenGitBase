# CI/CD PRD completion — work items

Vertical slices to close the gap between the ci-01…ci-20 tracer bullets and the full [PRD: CI/CD Pipelines](../../docs/prd/ci-cd-pipelines.md).

Phase-1 issues live in [`docs/issues/ci-cd-pipelines/`](../../docs/issues/ci-cd-pipelines/). These slices (**ci-prd-01…15**) finish operator surfaces, real Firecracker/OverlayFS execution, promotion, egress enforcement, and E2E gates.

**Out of scope (v1 PRD):** MR pipeline triggers, secrets, artifacts (stories 69–71).

## Status

| ID | Title | Type | Status | Blocked by |
|----|-------|------|--------|------------|
| ci-prd-01 | [Platform compute bootstrap + admin fleet UI](./items/ci-prd-01-platform-compute-bootstrap-admin-ui.md) | AFK | ready | — |
| ci-prd-02 | [Org compute settings UI + enrollment API hardening](./items/ci-prd-02-org-compute-settings-ui.md) | AFK | ready | — |
| ci-prd-03 | [Org compute enroll → job routing integration test](./items/ci-prd-03-org-compute-integration-test.md) | AFK | ready | ci-prd-02 |
| ci-prd-04 | [Base image catalog build + Layer Store artifacts](./items/ci-prd-04-base-image-build-layer-store.md) | AFK | ready | — |
| ci-prd-05 | [Firecracker MicroVM executor + operator requirements](./items/ci-prd-05-firecracker-executor.md) | HITL + AFK | ready | — |
| ci-prd-06 | [OverlayFS stack assembly in compute agent](./items/ci-prd-06-overlayfs-stack-assembly.md) | AFK | ready | ci-prd-04, ci-prd-05 |
| ci-prd-07 | [Firecracker `ogb-hosted` tracer](./items/ci-prd-07-firecracker-ogb-hosted-tracer.md) | AFK | ready | ci-prd-06 |
| ci-prd-08 | [Layer promotion runtime + promoted layer mount](./items/ci-prd-08-layer-promotion-runtime.md) | AFK | ready | ci-prd-07 |
| ci-prd-09 | [Host egress enforcement in compute agent](./items/ci-prd-09-host-egress-enforcement.md) | AFK | ready | ci-prd-07 |
| ci-prd-10 | [Job Identity security contract tests](./items/ci-prd-10-job-identity-security-tests.md) | AFK | ready | — |
| ci-prd-11 | [Admin CI console: base images + promotion dashboard](./items/ci-prd-11-admin-ci-base-images-promotion.md) | AFK | ready | ci-prd-04, ci-prd-08 |
| ci-prd-12 | [Admin + org domain allowance review UI](./items/ci-prd-12-domain-allowance-review-ui.md) | AFK | ready | ci-prd-02 |
| ci-prd-13 | [Pipeline log visibility + live streaming UI](./items/ci-prd-13-pipeline-log-streaming-acl.md) | AFK | ready | — |
| ci-prd-14 | [Compose bootstrap E2E gate](./items/ci-prd-14-compose-bootstrap-e2e-gate.md) | AFK | ready | ci-prd-01, ci-prd-07 |
| ci-prd-15 | [Community-hosted hybrid tracer](./items/ci-prd-15-community-hosted-tracer.md) | AFK | ready | ci-prd-03, ci-prd-07 |

## Dependency graph

```
ci-prd-01 ──────────────────────────────┐
ci-prd-02 ─→ ci-prd-03 ────────────────┤
ci-prd-04 ─┬→ ci-prd-06 ─→ ci-prd-07 ─┼→ ci-prd-08
ci-prd-05 ─┘                           ├→ ci-prd-09
                                       ├→ ci-prd-14
                                       └→ ci-prd-15
ci-prd-04, ci-prd-08 → ci-prd-11
ci-prd-02 → ci-prd-12
ci-prd-10, ci-prd-13 (parallel anytime)
```
