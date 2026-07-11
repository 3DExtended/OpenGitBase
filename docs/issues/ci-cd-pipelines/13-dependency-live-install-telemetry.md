# Dependency live install + telemetry

## Metadata

- ID: ci-13
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Execute ordered **Dependency Recipes** inside the **Job Sandbox**: for each `dependencies:` entry, check for a promoted **Dependency Layer** (ci-14); if absent, run `installscript` as root in the live MicroVM. Record install outcomes (success/failure, duration) for promotion analytics. Logs show layer hit vs live install per dependency. Support opt-in container tooling (e.g. Docker) only when declared as a dependency recipe.

## Acceptance criteria

- [ ] Dependencies install in YAML order before the job `script` runs
- [ ] `installscript` runs as root inside the guest
- [ ] When no promoted layer exists, live install runs and upper OverlayFS captures changes
- [ ] Logs include per-dependency section: promoted layer mount vs live install
- [ ] Install outcomes persisted (success, duration, recipe hash) for admin analytics
- [ ] Docker (or declared container tooling) available only when listed in `dependencies:`
- [ ] Integration test: recipe without promotion runs installscript; logs show live install

## Blocked by

- [10-first-ogb-hosted-job-tracer.md](./10-first-ogb-hosted-job-tracer.md) (ci-10)

## User stories covered

- 14 — Ordered `dependencies:` with `installscript`.
- 15 — Dependency `version` labels for humans only.
- 17 — Logs show layer hit vs live install.
- 18 — Opt into Docker via a dependency recipe.
- 68 — Dependency install outcomes recorded.

## Notes

- Layer key is `sha256(base_image_slug + normalized_installscript)` per PRD.
- Promoted layer mount path is ci-14.
