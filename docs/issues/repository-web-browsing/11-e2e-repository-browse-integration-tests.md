<!-- forge: #212 -->

# End-to-end repository browse integration tests

## Metadata

- ID: repo-browse-11
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

Add an automated **end-to-end integration test script** (shell + curl and/or `dotnet test` integration suite) that exercises the full repository browse path against Docker Compose (or the E2ETest harness where applicable).

Cover the critical paths from the PRD in one runnable check suitable for CI or local `scripts/test-repo-browse-e2e.sh`.

## Acceptance criteria

- [ ] **Public repo**: anonymous fetch root tree → 200 with entries; fetch readme → 200
- [ ] **Private repo**: anonymous tree → 404; signed-in outsider → 403; member → 200
- [ ] **Empty repo**: branches empty; UI/API empty state (no 500)
- [ ] **README precedence**: fixture with `README.md` returns expected file
- [ ] **1 MB cap**: oversized blob returns `isTooLarge` without inline body
- [ ] **SVG**: blob classified as download-only / no inline preview kind
- [ ] **Web replica routing**: content request never targets primary node (assert via log, mock, or test double)
- [ ] **Cache headers**: public response has `Cache-Control: public`; private has `no-store`
- [ ] **Rate limit**: burst anonymous requests eventually receive 429 (optional smoke subtest)
- [ ] Script documented in `scripts/` with one-line README mention
- [ ] Test is runnable in CI or documented as manual gate until CI wired

## Blocked by

- [03-private-repository-content-authorization.md](./03-private-repository-content-authorization.md)
- [05-readme-on-repository-home.md](./05-readme-on-repository-home.md)
- [06-blob-view-text-download-size-cap.md](./06-blob-view-text-download-size-cap.md)
- [07-blob-preview-images-markdown-toggle.md](./07-blob-preview-images-markdown-toggle.md)
- [08-web-replica-routing-syncing-banner.md](./08-web-replica-routing-syncing-banner.md)
- [09-redis-cache-rate-limits.md](./09-redis-cache-rate-limits.md)

## User stories covered

- 45

## Notes

- Follow pattern from `scripts/test-ha-storage-e2e.sh` and git-https e2e issues.
- Seed fixture repos during test setup (public with README + nested path + small/large/svg files; private; empty).
- Fail fast with clear assertion messages per scenario.
