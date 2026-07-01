# F01 auth regression matrix

## Metadata

- ID: pop-15
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Expand F01 to **≥50 `@Regression` scenarios** using auth matrix theories + lifecycle tests.

Add **~40 regression scenarios** including:

- Register validation matrix (duplicate email, weak password, missing fields)
- Account deletion → login denied
- Invite accept / decline via token
- Rate-limit smoke on login (429, `@Slow`)
- Captured email baselines: verify, reset, invite HTML templates
- Sign-in edge cases and token invalidation paths
- Matrix on account mutating endpoints (anonymous vs authenticated)

## Acceptance criteria

- [ ] F01 catalog shows ≥50 regression rows `done`
- [ ] Auth matrix theory class covers ≥25 rows
- [ ] Email template baselines committed for 3 template types
- [ ] `--tag Regression --feature Auth` runnable subset passes

## Blocked by

- [14-f01-auth-smoke-pack.md](./14-f01-auth-smoke-pack.md)
- [04-auth-matrix-theory-runner.md](./04-auth-matrix-theory-runner.md)

## User stories covered

- 33–37, 28–31 (regression depth)

## Notes

- Target 55 total F01 per PRD; smoke 10 + regression 45+.
