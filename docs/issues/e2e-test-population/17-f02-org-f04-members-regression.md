# F02 org + F04 members regression

## Metadata

- ID: pop-17
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Expand F02 and F04 to **≥50 `@Regression` scenarios each** via auth matrix theories and edge cases.

**F02 regression additions (~40):**

- Org CRUD validation (duplicate slug, rename)
- Invite resend, revoke, expired token
- Member demote, leave, role matrix on all org endpoints
- Org delete with blockers (repos remain)
- Cross-org outsider matrix

**F04 regression additions (~40):**

- Role change effects on MR approve, push, browse
- Add by username vs email paths
- Matrix: anonymous/outsider/reader/writer/admin/owner on all member endpoints
- Bulk membership edge cases

## Acceptance criteria

- [ ] F02 catalog ≥50 regression rows `done`
- [ ] F04 catalog ≥50 regression rows `done`
- [ ] Auth matrix theories per feature
- [ ] Cross-feature test: role change affects git clone permission

## Blocked by

- [16-f02-org-f04-members-smoke.md](./16-f02-org-f04-members-smoke.md)
- [04-auth-matrix-theory-runner.md](./04-auth-matrix-theory-runner.md)

## User stories covered

- 38–44, 52–55 (full depth)

## Notes

- Largest greenfield regression slice; may span multiple PRs if needed — keep catalog updated per PR.
