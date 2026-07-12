# Admin CI console: base images + promotion dashboard

## Metadata

- ID: ci-prd-11
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (PRD web surfaces — admin)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Admin console surfaces for **Base Image Catalog** management and **Dependency Layer Promotion**: list catalog slugs, add entries, view dependency install analytics (count, success rate, median duration), and trigger promotion when eligible.

Operators should manage CI supply chain without curl.

## Acceptance criteria

- [ ] Admin page lists base image catalog entries with slug, version label, and build status
- [ ] Admin can create new catalog entry (API already exists)
- [ ] Promotion dashboard lists dependency recipes with install stats from `DependencyInstallOutcome` data
- [ ] UI shows promotion eligibility (last five installs succeeded) and blocked state with reason
- [ ] Admin can request promotion; UI reflects queued/running/completed/failed status
- [ ] Visual test or Playwright coverage for admin CI console happy path

## Blocked by

- [ci-prd-04-base-image-build-layer-store.md](./ci-prd-04-base-image-build-layer-store.md)
- [ci-prd-08-layer-promotion-runtime.md](./ci-prd-08-layer-promotion-runtime.md)

## User stories covered

- 46 — Manage **Base Image Catalog**
- 48 — Dependency usage analytics
- 49 — Promotion blocked unless last five installs succeeded
- 50 — Layer promotion on platform nodes

## Notes

- Separate from platform compute fleet UI (ci-prd-01); link from admin nav as "CI" or sub-sections.
- Depends on ci-prd-08 so promotion UI reflects real build outcomes, not only DB `Queued` rows.
