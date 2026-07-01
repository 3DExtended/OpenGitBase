# F05 browse parity smoke

## Metadata

- ID: pop-09
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md) · Parity: [repo-browse-11](../repository-web-browsing/11-e2e-repository-browse-integration-tests.md)

## What to build

Close **repo-browse-11 smoke** gaps in C# E2E — **10 `@Smoke` scenarios** for repository content browse.

Minimum scenarios:

1. Public refs + tree anonymous 200 (extend existing)
2. Private anon 404, outsider 403, member 200
3. Empty repo refs without 500
4. README endpoint returns fixture content
5. Blob under 1MB inline
6. Blob over 1MB `isTooLarge`
7. SVG download-only classification
8. `Cache-Control: public` on public response
9. `Cache-Control: no-store` on private member response
10. Raw blob download smoke

Each scenario: transcript, committed baselines, catalog row.

## Acceptance criteria

- [ ] ≥10 smoke scenarios with `[Trait("Smoke")]`
- [ ] repo-browse-11 acceptance items 1–7 covered
- [ ] Catalog rows `E2E-F05-001` … updated to `done`
- [ ] `dotnet run … -- --feature Repository --tag Smoke` passes

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)
- [03-git-testdata-provisioning.md](./03-git-testdata-provisioning.md)

## User stories covered

- 56–64 (browse smoke subset)

## Notes

- Rate-limit 429 (repo-browse-11 item 9) deferred to regression slice pop-20 (`@Slow`).
