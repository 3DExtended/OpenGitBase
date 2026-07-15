<!-- forge: #100 -->

# F08 git HTTPS smoke expansion

## Metadata

- ID: pop-12
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Expand **F08 git HTTPS** to **10 `@Smoke` scenarios** beyond existing PAT push/clone.

Minimum scenarios:

1. Write PAT push + clone (existing)
2. Read PAT push denied (existing)
3. PAT create / list / revoke API lifecycle
4. Revoked PAT denied on git
5. Org-owned repo HTTPS push (parity with integration script)
6. Push to protected branch denied
7. `git fetch` after push updates refs
8. Invalid PAT → 401 on git
9. Access-check API spot-check (writer allowed)
10. Clone empty repository

## Acceptance criteria

- [ ] ≥10 smoke scenarios with git + API baselines
- [ ] Org repo push scenario passes
- [ ] PAT revoke propagates to git within test
- [ ] Catalog F08 smoke rows complete

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)

## User stories covered

- 87–94 (smoke subset)

## Notes

- Protected branch case coordinates with MR fixture from pop-10.
