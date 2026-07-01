# F06 discussion parity smoke

## Metadata

- ID: pop-11
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md) · Parity: [disc-10](../repository-discussions/10-e2e-discussion-integration-tests.md)

## What to build

Close **disc-10 smoke gaps** — **10 `@Smoke` scenarios** for discussions.

Minimum scenarios (extend existing 3):

1. Public anon read; create 401 (existing)
2. Private anon 404; outsider 403; member 200 (existing)
3. Comment → Engaged → resolve (existing)
4. Reopen via comment without re-Engage
5. Block user → cannot comment; can read
6. Unblock restores comment
7. In-app notification on comment (API record)
8. Email subject `[owner/repo #n]` in captured mail
9. Tag filter list smoke
10. Anchored comment located on fixture repo

## Acceptance criteria

- [ ] disc-10 acceptance items 1–7 covered at smoke level
- [ ] ≥10 smoke scenarios with baselines
- [ ] Email side-channel baselines for notification subject
- [ ] Catalog updated for F06 smoke rows

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)

## User stories covered

- 65–72

## Notes

- Sub-thread depth deferred to pop-21 regression.
