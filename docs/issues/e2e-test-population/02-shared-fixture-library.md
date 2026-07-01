# Shared fixture library

## Metadata

- ID: pop-02
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Extract and centralize E2E setup into a **fixture library** in the E2E core/support layer.

Fixtures (minimum):

1. **IdentityFixture** — admin, writer, outsider JWT clients (extend existing seed patterns).
2. **RepositoryFixture** — create public/private/empty repo; return slug, id, owner client.
3. **OrganizationFixture** — create org, add member, return org slug and clients.
4. **MergeRequestFixture** — repo with protected `main`, feature branch with commits, MR-ready state.
5. **PatFixture** — create write/read PAT; build HTTPS remote URL.
6. **EmailCapture** — poll/clear internal E2E mail API; parse verification codes.
7. **GitOperations** — consolidate push/clone/assert-refs helpers (extend existing facade).

All fixtures use **per-test run suffix** from `TestIsolation`; no cross-test mutable shared state.

## Acceptance criteria

- [ ] Fixtures usable from at least one refactored existing E2E test (e.g. discussion seed)
- [ ] Unit smoke test or existing E2E still passes after refactor
- [ ] Fixtures documented in E2E README with usage examples
- [ ] Email and git helpers reduce duplication in auth and HTTPS tests

## Blocked by

- None — can start immediately

## User stories covered

- 6, 7, 14, 15

## Notes

- Critical path blocker for almost all population slices.
- Do not reintroduce full DB truncate between tests.
