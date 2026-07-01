# F03 repository settings regression

## Metadata

- ID: pop-19
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Expand F03 to **≥50 `@Regression` scenarios**.

Additions:

- Push rules: forbidden paths, DCO requirement with git push denial + message substring
- Protected branch variants (require approvals, admin allowlist direct push)
- Settings matrix on all mutating endpoints
- Slug conflict, rename edge cases
- List repos pagination smoke
- Transfer ownership (if supported) or documented skip

## Acceptance criteria

- [ ] F03 catalog ≥50 regression rows `done`
- [ ] Push rule enforcement proven via git HTTPS
- [ ] Auth matrix theory for repository settings endpoints
- [ ] Coordinates with F07 protected branch scenarios

## Blocked by

- [18-f03-repository-settings-smoke.md](./18-f03-repository-settings-smoke.md)
- [04-auth-matrix-theory-runner.md](./04-auth-matrix-theory-runner.md)

## User stories covered

- 45–51 (full depth)

## Notes

- Git push rule tests reuse `PatFixture` and `GitOperations`.
