# F09 SSH git profile scenarios

## Metadata

- ID: pop-28
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Implement **≥50 `@Regression` scenarios** for SSH git (F09) under **optional SSH compose profile**.

**Smoke (10):**

1. SSH public key create
2. Fingerprint lookup API
3. Clone over SSH
4. Push over SSH
5. Unauthorized key rejected
6. Revoked key rejected
7. Reader can clone private repo
8. Outsider clone denied
9. Push to protected branch denied
10. SSH profile skip message on fast/default compose

**Regression (~40):**

- Mirror critical F08 HTTPS scenarios over SSH
- Dispatcher routing smoke
- Key rotation (add second key, revoke first)
- Org repo SSH paths
- Auth matrix on SSH key CRUD endpoints

Default compose **skips** F09 with recorded reason unless SSH profile enabled.

## Acceptance criteria

- [ ] F09 catalog ≥50 rows `done`
- [ ] SSH compose profile documented in E2E README
- [ ] Scenarios skip cleanly when `GIT_SSH` profile not active
- [ ] git-storage-proxy e2e parity referenced in catalog

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)
- [23-f08-git-https-regression.md](./23-f08-git-https-regression.md)

## User stories covered

- 95–98

## Notes

- Lower priority than HTTPS; may ship after Wave 3 if SSH remains optional in dev docs.
