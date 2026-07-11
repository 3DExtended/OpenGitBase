# Layer promotion admin + promoted mounts

## Metadata

- ID: ci-14
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Platform-admin **Layer Promotion** workflow: view dependency usage analytics (install count, success rate, median duration), promote a recipe to a shared **Dependency Layer** in the Layer Store when the last five installs succeeded, and mount promoted layers automatically on subsequent jobs. Promotion builds run only on **Platform Compute Nodes**. Promoted layers are delta overlays from the base image only.

## Acceptance criteria

- [ ] Admin UI/API lists dependency recipes with install stats (count, success rate, duration)
- [ ] Promotion blocked unless last five install outcomes for the recipe succeeded
- [ ] Promotion job runs on a platform node and uploads layer artifact to Layer Store
- [ ] Subsequent jobs mount promoted layer in OverlayFS stack instead of running `installscript`
- [ ] Logs show promoted layer hit for matching recipes
- [ ] Non-admin users cannot trigger promotion
- [ ] Integration test: five successful installs → promote → sixth job skips live install

## Blocked by

- [13-dependency-live-install-telemetry.md](./13-dependency-live-install-telemetry.md) (ci-13)
- [09-base-image-catalog-layer-store.md](./09-base-image-catalog-layer-store.md) (ci-09)

## User stories covered

- 16 — Promoted layers used automatically when available.
- 48 — Dependency usage analytics for promotion decisions.
- 49 — Promotion blocked unless last five installs succeeded.
- 50 — Layer Promotion builds only on platform nodes.

## Notes

- Recipe identity uses normalized installscript + base image slug hash.
- Node-local layer cache reduces repeated S3 fetches (story 51 partial).
