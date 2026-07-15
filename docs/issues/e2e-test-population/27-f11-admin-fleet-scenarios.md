<!-- forge: #115 -->

# F11 admin fleet smoke + regression

## Metadata

- ID: pop-27
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md) · Related: [admin-replication-ui PRD](../../prd/admin-replication-ui.md)

## What to build

Implement **≥50 `@Regression` scenarios** for admin fleet & replication (F11), including **10 `@Smoke`**.

**Smoke minimum:**

1. Admin login; list storage nodes
2. Outsider denied on `/admin/*`
3. Replication summary after repo create
4. Per-repo replication detail endpoint
5. Storage enrollment admin API smoke
6. Healthy node count matches fleet
7. Degraded flag on injected failure (coordinate with HA chaos)
8. Non-admin writer denied on admin routes
9. Admin storage node detail response shape
10. Fleet dispatcher SSH key generation smoke

**Regression additions:**

- Full auth matrix on admin endpoints
- Attention rules and severity sort API (per admin PRD)
- Replication state after push and quorum events
- Multiple repository replication statuses in summary

## Acceptance criteria

- [ ] F11 catalog ≥50 rows `done` (10 smoke + 40 regression)
- [ ] Admin-only matrix enforced on all admin routes tested
- [ ] API-level coverage satisfies admin-replication-ui PRD testing decisions
- [ ] Degraded-state scenario coordinates with F10 chaos helpers

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)
- [26-f10-ha-full-regression.md](./26-f10-ha-full-regression.md)

## User stories covered

- 107–110

## Notes

- Playwright admin page smoke deferred to pop-29.
- Some scenarios require full-HA fleet state — tag `@FullHa` where needed.
