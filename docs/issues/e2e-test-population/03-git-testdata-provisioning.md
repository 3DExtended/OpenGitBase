<!-- forge: #91 -->

# Git testdata provisioning

## Metadata

- ID: pop-03
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Provision **stable git fixture repositories** for browse, discussion anchor, and MR diff scenarios.

1. Define a known file tree: `README.md`, nested `src/foo/bar.txt`, blob >1MB, SVG file, anchor target lines.
2. **Seed once per fixture class** via git push (HTTPS PAT) during test setup — not committed as large baselines.
3. Expose fixture API: `GetBrowsePublicRepo()`, `GetAnchorRepo()`, etc. returning owner/slug and expected path outcomes.
4. Document provisioning wait/retry for storage fleet readiness.

Verifiable: browse smoke can assert README content, size cap, and SVG classification against known tree.

## Acceptance criteria

- [ ] Fixture repos created programmatically with documented tree layout
- [ ] At least one E2E test uses fixture for README or nested path assertion
- [ ] Oversized blob and SVG paths exist in seeded repo
- [ ] Provisioning integrated with `RepositoryFixture` / `PatFixture`

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)

## User stories covered

- 8, 59, 60, 61, 72

## Notes

- Required for repo-browse-11 parity (pop-09) and discussion anchors (pop-11).
