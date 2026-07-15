<!-- forge: #101 -->

# F10 HA parity smoke

## Metadata

- ID: pop-13
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md) · Parity: [ha-storage-12](../ha-storage-replication/12-end-to-end-ha-integration-tests.md)

## What to build

Close **ha-storage-12 smoke** under **`--profile full-ha`** — **5 `@Smoke` `@FullHa` scenarios**.

Minimum scenarios:

1. Stop storage node; API health remains (existing)
2. RF=3 bare repos exist on three nodes after create
3. Push increments watermarks on ≥2 nodes
4. Stop non-primary node; quorum push still succeeds
5. Clone/fetch succeeds after push (read path smoke)

Uses chaos helpers and storage health probes.

## Acceptance criteria

- [ ] ha-storage-12 items 1, 2, 3, 5 covered at smoke level
- [ ] Scenarios tagged `FullHa` and `Smoke`
- [ ] Fast profile skips these with clear reason
- [ ] Full-ha runner smoke passes

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)
- [08-full-ha-tier-gating.md](./08-full-ha-tier-gating.md)

## User stories covered

- 99–105 (smoke subset)

## Notes

- Primary failover (ha-storage-12 #4) deferred to pop-26.
