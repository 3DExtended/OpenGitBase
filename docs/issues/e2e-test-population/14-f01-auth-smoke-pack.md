# F01 auth smoke pack

## Metadata

- ID: pop-14
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Implement **10 `@Smoke` scenarios** for auth & account (F01).

Minimum scenarios:

1. Register → email → verify → login (maintain existing journey)
2. Seed personas exist (maintain existing)
3. Login wrong password → 401
4. Login unverified → 403
5. Password reset email → reset → login
6. Change password success
7. Change password wrong current → 400
8. `GET /account/me` anonymous → 401
9. Resend verification → second captured email
10. Sign-out → subsequent API 401

Email side-channel baselines for verify and reset templates.

## Acceptance criteria

- [ ] 10 smoke scenarios with committed baselines
- [ ] Email capture used for reset and resend flows
- [ ] Catalog F01 smoke rows marked done
- [ ] No regression in existing auth journey test

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)
- [04-auth-matrix-theory-runner.md](./04-auth-matrix-theory-runner.md)

## User stories covered

- 26–32, 35 (smoke subset)

## Notes

- Invite flows in pop-15 regression; rate-limit in pop-15.
