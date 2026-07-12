# Base image catalog build + Layer Store artifacts

## Metadata

- ID: ci-prd-04
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-09)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Close the loop between **Base Image Catalog** slugs and runnable rootfs artifacts in the **Layer Store** (MinIO). Platform operators (or seed/bootstrap) build pinned OCI sources into content-addressed rootfs blobs; compute agents resolve `image:` slugs to downloaded artifacts for sandbox assembly.

## Acceptance criteria

- [ ] Catalog entry includes pinned OCI source, content hash, and Layer Store object key
- [ ] Build or import path produces rootfs artifact for at least one seed slug (e.g. alpine catalog entry)
- [ ] Agent (or shared resolver service) fetches artifact by slug from Layer Store with hash verification
- [ ] Missing or unknown slug fails job preparation with clear log message
- [ ] Integration test: catalog slug resolves to fetchable artifact in compose MinIO
- [ ] Seed data or bootstrap documents how to add new slugs in dev

## Blocked by

- None — can start immediately (MinIO foundation from ci-01)

## User stories covered

- 46 — Platform admin manages **Base Image Catalog**
- 47 — Base images built from pinned OCI sources
- 51 — Layers stored in S3-compatible **Layer Store** with node caching

## Notes

- Admin create API exists; this slice adds the **build/import pipeline** and agent fetch path, not just DB rows.
- Promotion layers (ci-prd-08) depend on this bottom-layer artifact model.
