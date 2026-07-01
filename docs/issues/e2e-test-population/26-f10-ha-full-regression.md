# F10 HA full regression

## Metadata

- ID: pop-26
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md) · Parity: [ha-storage-12](../ha-storage-replication/12-end-to-end-ha-integration-tests.md)

## What to build

Expand F10 to **≥50 `@Regression` `@FullHa` scenarios**.

Additions beyond smoke:

- Primary storage stop → promotion → push resumes (ha-storage-12 #4)
- Delete repository with one node down; DB + disk cleanup
- Rebalance when node returns (stretch)
- Concurrent pushes same repo
- Admin replication metadata matches fleet state after operations
- Watermark drift detection smoke
- Read replica routing metadata assertions
- Chaos transcript patterns for each failure injection

## Acceptance criteria

- [ ] F10 catalog ≥50 regression rows `done`
- [ ] ha-storage-12 all acceptance criteria met
- [ ] Full-ha profile documented as required
- [ ] Fast profile skips entire F10 regression set

## Blocked by

- [13-f10-ha-parity-smoke.md](./13-f10-ha-parity-smoke.md)
- [08-full-ha-tier-gating.md](./08-full-ha-tier-gating.md)

## User stories covered

- 99–106 (full depth)

## Notes

- Longest-running slice; tag many as `@Slow` where appropriate.
