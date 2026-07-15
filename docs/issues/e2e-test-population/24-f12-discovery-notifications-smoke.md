<!-- forge: #112 -->

# F12 discovery + notifications smoke

## Metadata

- ID: pop-24
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Greenfield **10 `@Smoke` scenarios** for public discovery & notifications (F12).

Minimum scenarios:

1. `GET /public/repositories` shape smoke
2. `GET /public/recent` smoke
3. `GET` public owner profile by slug
4. Anonymous explore data consistent with API
5. `GET /notifications` authenticated
6. Unread count endpoint
7. Mark notification read
8. MR event creates notification record
9. Discussion comment creates notification record
10. Notification list empty state for new user

## Acceptance criteria

- [ ] 10 smoke scenarios with API baselines
- [ ] Cross-links to F06/F07 notification triggers
- [ ] Catalog F12 smoke complete
- [ ] New category trait `Discovery` or split `Notifications`

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)

## User stories covered

- 111–114 (smoke subset)

## Notes

- Playwright explore page deferred to pop-29.
