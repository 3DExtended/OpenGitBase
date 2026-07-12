# Compose bootstrap E2E gate

## Metadata

- ID: ci-prd-14
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-20)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Make **`scripts/test-pipelines-e2e.sh`** a reliable automated gate: compose bootstrap enrolls platform compute agent without manual token paste, fixture push produces a green Firecracker-backed `ogb-hosted` run, and the script is documented as the canonical local/CI verification step.

## Acceptance criteria

- [ ] Bootstrap flow provisions platform compute enrollment token into compose override automatically
- [ ] `test-pipelines-e2e.sh` passes against full compose stack without manual steps
- [ ] Script asserts run status passed, expected job count, and non-empty structured logs
- [ ] README or operator doc references script as post-change CI/CD smoke test
- [ ] Failure modes documented (KVM missing, agent unhealthy, Kafka down)

## Blocked by

- [ci-prd-01-platform-compute-bootstrap-admin-ui.md](./ci-prd-01-platform-compute-bootstrap-admin-ui.md)
- [ci-prd-07-firecracker-ogb-hosted-tracer.md](./ci-prd-07-firecracker-ogb-hosted-tracer.md)

## User stories covered

- 21 — Push triggers pipeline run
- 63 — Storage hooks report push metadata only
- 64 — Kafka event backbone
- 65 — Idempotent pipeline creation per commit
- 66 — Postgres as job state of record

## Notes

- ci-20 added the script; this slice wires bootstrap + Firecracker path so the gate is trustworthy.
- Optional: add to CI workflow if repository has suitable runner with KVM.
