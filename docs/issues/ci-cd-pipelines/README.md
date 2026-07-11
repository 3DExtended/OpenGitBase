# CI/CD pipelines — implementation issues

Vertical slices for [PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md).

Implement in dependency order; each issue lists explicit blockers.

| ID | Issue | Type | Status | Blocked by |
|----|-------|------|--------|------------|
| ci-01 | [Compose foundation: Kafka + MinIO](./01-compose-kafka-minio-foundation.md) | AFK | ready | — |
| ci-02 | [Pipeline YAML parser + v1 validation](./02-pipeline-yaml-parser.md) | AFK | ready | — |
| ci-03 | [Push trigger → Pipeline Run (no execution)](./03-push-trigger-pipeline-run.md) | AFK | ready | ci-01, ci-02 |
| ci-04 | [Pipeline run read API + empty state](./04-pipeline-run-api-empty-state.md) | AFK | ready | ci-03 |
| ci-05 | [Compute node registry + platform enrollment](./05-compute-node-registry-platform-enrollment.md) | AFK | ready | ci-01 |
| ci-06 | [Org Owner self-service compute enrollment](./06-org-compute-self-service-enrollment.md) | AFK | ready | ci-05 |
| ci-07 | [Job queue, claim API, Job Identity](./07-job-queue-claim-job-identity.md) | AFK | ready | ci-03, ci-05 |
| ci-08 | [Compute agent runtime](./08-compute-agent-runtime.md) | AFK | ready | ci-07 |
| ci-09 | [Base Image Catalog + Layer Store seed](./09-base-image-catalog-layer-store.md) | AFK | ready | ci-01, ci-05 |
| ci-10 | [Tracer: first `ogb-hosted` job end-to-end](./10-first-ogb-hosted-job-tracer.md) | AFK | ready | ci-07, ci-08, ci-09 |
| ci-11 | [Staged pipelines + `only` globs](./11-staged-pipelines-only-globs.md) | AFK | ready | ci-10 |
| ci-12 | [CI variables + `GIT_DEPTH` materialization](./12-ci-variables-git-depth.md) | AFK | ready | ci-10 |
| ci-13 | [Dependency live install + telemetry](./13-dependency-live-install-telemetry.md) | AFK | ready | ci-10 |
| ci-14 | [Layer promotion admin + promoted mounts](./14-layer-promotion-admin.md) | AFK | ready | ci-13, ci-09 |
| ci-15 | [Hybrid `runs-on` routing](./15-hybrid-runs-on-routing.md) | AFK | ready | ci-10, ci-06 |
| ci-16 | [Egress allowlists + domain requests](./16-egress-allowlists-domain-requests.md) | AFK | ready | ci-10, ci-06 |
| ci-17 | [Platform agent Kafka job wake](./17-platform-agent-kafka-wake.md) | AFK | ready | ci-08, ci-07 |
| ci-18 | [Job timeout, cancel, resource limits](./18-job-timeout-cancel-limits.md) | AFK | ready | ci-10 |
| ci-19 | [Pipeline UI: detail, logs, cancel, commit badge](./19-pipeline-ui-detail-logs.md) | AFK | ready | ci-10, ci-04 |
| ci-20 | [Compose E2E: push → green pipeline](./20-compose-e2e-push-to-green.md) | AFK | ready | ci-11, ci-19 |

## Dependency graph

```
ci-01 ─┬→ ci-03 ─→ ci-04 ────────────────┐
       │         ↘                         │
ci-02 ─┘           ci-07 ─→ ci-08 ─┐       │
ci-01 ─→ ci-05 ─→ ci-06 ───────────┼→ ci-10 ─┬→ ci-11 ─┐
       │         ↘                 │         ├→ ci-12  │
ci-01 ─┴→ ci-09 ───────────────────┘         ├→ ci-13 ─→ ci-14
                                              ├→ ci-15
                                              ├→ ci-16
                                              ├→ ci-18
                                              └→ ci-19 ─→ ci-20
ci-07, ci-08 → ci-17
```

## Out of scope (v1)

User stories 69–71 (MR pipelines, secrets, artifacts) are deferred per the PRD.

## Source

- [docs/prd/ci-cd-pipelines.md](../../prd/ci-cd-pipelines.md)
- [docs/adr/0001-pipeline-trigger-event-bus.md](../../adr/0001-pipeline-trigger-event-bus.md)
- [CONTEXT.md](../../../CONTEXT.md)
