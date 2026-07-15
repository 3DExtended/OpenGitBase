<!-- forge: #111 -->

# F08 git HTTPS regression

## Metadata

- ID: pop-23
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Expand F08 to **≥50 `@Regression` scenarios**.

Additions beyond smoke:

- Multiple PATs; scope least-privilege matrix
- Access-check API full role matrix
- Wrong repo slug on git remote
- Push tags; fetch tags
- Large push smoke (within fast profile)
- HTTPS routing via HAProxy smoke
- PAT expiry edge (if supported)
- Matrix: git operations per repository role

## Acceptance criteria

- [ ] F08 catalog ≥50 regression rows `done`
- [ ] Git transcript on all failure paths
- [ ] LibGit2Sharp ref assertions on clone outcomes
- [ ] Retired `e2e-https-git-test.sh` parity fully superseded

## Blocked by

- [12-f08-git-https-smoke-expansion.md](./12-f08-git-https-smoke-expansion.md)
- [04-auth-matrix-theory-runner.md](./04-auth-matrix-theory-runner.md)

## User stories covered

- 87–94 (full depth)

## Notes

- Org repo scenarios coordinate with pop-16.
