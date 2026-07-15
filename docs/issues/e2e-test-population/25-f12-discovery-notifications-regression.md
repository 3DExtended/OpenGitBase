<!-- forge: #113 -->

# F12 discovery + notifications regression

## Metadata

- ID: pop-25
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Expand F12 to **≥50 `@Regression` scenarios**.

Additions:

- Public discovery pagination and filtering
- Owner profile variants (empty, many repos)
- Notification fan-out matrix (MR merge, discussion mention, subscribe/unsubscribe)
- Mark all read; notification payload shape baselines
- Anonymous denied on notification endpoints
- Cross-feature: verify notification after F07 merge and F06 comment
- Auth matrix on discovery endpoints

## Acceptance criteria

- [ ] F12 catalog ≥50 regression rows `done`
- [ ] ≥5 cross-feature notification scenarios with F06/F07
- [ ] Matrix theory ≥15 rows on notification API
- [ ] Regression tag filter passes for F12

## Blocked by

- [24-f12-discovery-notifications-smoke.md](./24-f12-discovery-notifications-smoke.md)
- [04-auth-matrix-theory-runner.md](./04-auth-matrix-theory-runner.md)

## User stories covered

- 111–114 (full depth)

## Notes

- May share notification helpers with discussion slice.
