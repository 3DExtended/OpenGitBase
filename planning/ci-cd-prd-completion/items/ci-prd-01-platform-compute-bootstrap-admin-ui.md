# Platform compute bootstrap + admin fleet UI

## Metadata

- ID: ci-prd-01
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-05)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Deliver the **platform operator path** for compute nodes end to end: an admin UI to create enrollment tokens and list registered platform nodes, plus a compose bootstrap step that wires `compute-agent-1` with a valid token (mirroring how storage fleet enrollment is handled today).

An operator should be able to open the admin console, mint a platform compute enrollment, paste or auto-inject it into local compose override config, start the agent, and see the node appear as healthy and eligible for `ogb-hosted`.

## Acceptance criteria

- [x] Admin UI lists platform compute nodes with health, capacity, and last heartbeat
- [x] Admin UI creates enrollment tokens with required capacity fields (`MaxConcurrentJobs`, `MaxCpu`, `MaxMemoryBytes`)
- [x] Bootstrap script (or extension of existing fleet bootstrap) writes `ComputeAgent__EnrollmentToken` into compose override for the default platform agent
- [x] After bootstrap + compose up, platform agent registers and heartbeats successfully
- [x] Visual or API test covers admin enrollment create flow
- [x] Operator docs updated: no manual curl required for local dev platform compute

## Implementation record

- Branch: `main`
- Tests: compute node handler tests, API controller tests, `pnpm test`, `pnpm test:visual:update`
- Visual: `tests/visual/admin-compute.spec.ts`

## Blocked by

- None — can start immediately

## User stories covered

- 44 — Platform admin enrolls **Platform Compute Nodes**
- 45 — Platform agents use Kafka job notifications (agent already consumes; this slice completes operator onboarding)

## Notes

- Reuse patterns from `admin/storage` enrollment UX and `bootstrap-fleet.sh` token injection.
- Platform enrollment API already exists at `POST /admin/compute-nodes/enrollments`; this slice is UI + ops glue, not new registry schema.
