# F05 browse regression matrix

## Metadata

- ID: pop-20
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Expand F05 to **≥50 `@Regression` scenarios** beyond smoke pack.

Additions:

- Auth matrix on refs/tree/blob/readme/raw for all roles
- Nested path navigation matrix
- Binary blob content-type cases
- Ref not found 404; invalid path 400
- Rate-limit 429 burst (`@Slow`, optional)
- Cache header matrix expanded
- Empty + non-empty repo variants

## Acceptance criteria

- [ ] F05 catalog ≥50 regression rows `done`
- [ ] repo-browse-11 item 9 (rate limit) covered or explicitly deferred in catalog
- [ ] Matrix theory class ≥20 rows
- [ ] All scenarios use git testdata fixture

## Blocked by

- [09-f05-browse-parity-smoke.md](./09-f05-browse-parity-smoke.md)
- [04-auth-matrix-theory-runner.md](./04-auth-matrix-theory-runner.md)

## User stories covered

- 56–64 (full depth)

## Notes

- Web replica routing assertion (repo-browse-11 item 7) may require log probe or metadata API — document approach in catalog notes.
