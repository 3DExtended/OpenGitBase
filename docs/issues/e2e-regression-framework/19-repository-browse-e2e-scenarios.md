<!-- forge: #87 -->

# Repository browse E2E scenarios

## Metadata

- ID: e2e-19
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Migrate repository web browse shell e2e into C# tests under `Repository/` feature folder. Self-seed fixture repos via git push (public with README + nested paths + small/large/svg files; private; empty). Cover repo-browse-11 integration criteria:

1. **Public repo** — anonymous refs/tree 200; README when present.
2. **Private repo** — anonymous refs 404; outsider 403; member 200.
3. **Empty repo** — refs/tree empty state without 500.
4. **README precedence** — fixture returns expected readme file.
5. **Blob size cap** — oversized blob returns `isTooLarge` without inline body.
6. **SVG** — classified download-only / no inline preview kind.
7. **Cache headers** — public `Cache-Control: public`; private `no-store`.
8. **Rate limit** — burst anonymous requests eventually 429 (optional smoke subtest).

Extend baselines to include normalized HTML page snapshots where browse UI is fetched, plus API content responses.

Retire `test-repo-browse-e2e.sh` when baselines committed (e2e-20).

## Acceptance criteria

- [ ] Public anonymous content refs and tree succeed
- [ ] Private anonymous 404 and outsider 403 enforced
- [ ] Empty repository handled without server error
- [ ] README and blob cap behaviors match repo-browse PRD
- [ ] Cache-Control headers asserted in API baselines
- [ ] Fixtures created via git push in test setup (not manual env vars)
- [ ] Committed baselines for browse scenarios
- [ ] Parity with `test-repo-browse-e2e.sh` and repo-browse-11 criteria

## Blocked by

- [09-git-facade-https-pat-scenario.md](./09-git-facade-https-pat-scenario.md)

## User stories covered

- Repository web browsing PRD integration (repo-browse-11): public/private browse, empty repo, readme, blob cap, cache headers

## Notes

- Prior art: `scripts/test-repo-browse-e2e.sh`, [repo-browse-11](../repository-web-browsing/11-e2e-repository-browse-integration-tests.md).
- Web replica routing assertion optional if not observable without log plumbing — document skip if blocked.
- Page HTML baselines use normalizer from e2e-03/06.
