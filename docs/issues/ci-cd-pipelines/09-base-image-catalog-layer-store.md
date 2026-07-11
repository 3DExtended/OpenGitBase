# Base Image Catalog + Layer Store seed

## Metadata

- ID: ci-09
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Implement platform-admin management of the **Base Image Catalog**: slugs map to pinned rootfs artifacts in the **Layer Store** (built from traceable OCI provenance, not runtime `docker pull`). Seed at least one base image (e.g. Alpine) into MinIO. Agents resolve `image:` slugs to OverlayFS bottom-layer artifacts via Job Identity–scoped fetch.

## Acceptance criteria

- [ ] Admin API CRUD for base image catalog entries (slug, version label, artifact URI, OCI provenance metadata)
- [ ] Seed script uploads a pinned rootfs artifact for a default slug into MinIO
- [ ] Parser/catalog validation rejects unknown `image:` slugs at schedule time or claim time
- [ ] Agent can fetch base layer blob from Layer Store using Job Identity
- [ ] Catalog changes are auditable (who/when)
- [ ] Integration test: job spec with catalog slug resolves to fetchable artifact

## Blocked by

- [01-compose-kafka-minio-foundation.md](./01-compose-kafka-minio-foundation.md) (ci-01)
- [05-compute-node-registry-platform-enrollment.md](./05-compute-node-registry-platform-enrollment.md) (ci-05)

## User stories covered

- 3 — `image:` is a **Base Image Catalog** slug.
- 46 — Platform admin manages the **Base Image Catalog**.
- 47 — Base images built from pinned OCI sources.

## Notes

- Dependency **Layer Promotion** and promoted layer blobs are ci-14.
- Local node caching of layers can start as best-effort filesystem cache.
