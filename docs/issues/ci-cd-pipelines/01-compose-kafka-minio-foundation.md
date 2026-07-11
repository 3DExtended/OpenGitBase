# Compose foundation: Kafka + MinIO

## Metadata

- ID: ci-01
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Add the CI/CD infrastructure backbone to the local Docker Compose stack: a three-broker Apache Kafka cluster (KRaft mode) and an S3-compatible MinIO instance for the **Layer Store**. Wire the API and future CI services with connection settings, health checks, and bootstrap scripts so operators can start the full stack with one command. Topics `git.push.received` and `ci.job.available` are created with replication factor 3.

This slice is infrastructure-only — no pipeline logic yet. Success is demonstrable by starting compose and verifying Kafka and MinIO are healthy and reachable from the API container.

## Acceptance criteria

- [ ] Three Kafka brokers run in KRaft mode with RF=3 for CI topics
- [ ] MinIO runs with a dedicated bucket for dependency layers and base image artifacts
- [ ] API (or a shared env contract) documents `KAFKA_*` and `LAYER_STORE_*` connection variables
- [ ] Compose health checks pass for all new services within a normal `docker compose up`
- [ ] Bootstrap or init script creates `git.push.received` and `ci.job.available` topics if missing
- [ ] README or operator docs describe the new services and ports

## Blocked by

None — can start immediately.

## User stories covered

- 51 — As a platform admin, I want layers stored in S3-compatible **Layer Store** with node caching, so that all nodes benefit.
- 64 — As an operator, I want Kafka as the event backbone, so that new consumers can subscribe without hook changes.

## Notes

- Mirrors ADR 0001 (pipeline trigger event bus).
- Does not yet publish or consume events — that lands in ci-03 and ci-07.
