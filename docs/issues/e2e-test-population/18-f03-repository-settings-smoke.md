# F03 repository settings smoke

## Metadata

- ID: pop-18
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Greenfield **10 `@Smoke` scenarios** for repository lifecycle & settings (F03).

Minimum scenarios:

1. Create public repo
2. Create private repo
3. Update description/metadata
4. Anonymous GET public metadata 200
5. Anonymous GET private → 404
6. Set default branch
7. Add protected branch rule on `main`
8. Outsider denied on settings mutation
9. Usage/stats endpoint after push
10. Delete repo → browse 404

## Acceptance criteria

- [ ] 10 smoke scenarios with baselines
- [ ] Protected branch rule usable by MR tests
- [ ] Delete verifies API + browse denial
- [ ] Catalog F03 smoke complete

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)

## User stories covered

- 45–51 (smoke subset)

## Notes

- Push rules (DCO, forbidden paths) in pop-19.
