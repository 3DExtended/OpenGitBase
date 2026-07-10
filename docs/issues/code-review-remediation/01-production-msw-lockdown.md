# Production MSW and test artifact lockdown

## Metadata

- ID: sec-01
- Type: AFK
- Status: ready
- Source: code review (Jul 2026)

## What to build

Prevent the mock service worker and visual-test harness from activating or shipping in production builds.

**Behavior:**

- MSW must not start when `NUXT_PUBLIC_MSW` is false — including when a visitor appends `?msw=1` to the URL.
- Production bundles must not lazy-load `tests/mocks/*` or register `mockServiceWorker.js` as an activatable service worker.
- The `__visual__` component gallery route and related visual-only fixtures must be excluded from production routing (or unreachable without dev tooling).
- A build-time or CI check proves production mode ignores mock activation.

## Acceptance criteria

- [ ] Appending `?msw=1` to any production URL does not register or start MSW
- [ ] Production build artifacts do not include an activatable `mockServiceWorker.js` (or it is inert)
- [ ] `__visual__` gallery is not reachable in production deployments
- [ ] CI or build script fails or warns if MSW bypass paths remain in production code paths
- [ ] Existing Playwright visual tests still pass in dev/CI (MSW enabled only there)

## Blocked by

- None — can start immediately

## Findings covered

- Critical: MSW `?msw=1` bypass in production
- Medium: `__visual__` route public in production
- Low: test/dev artifacts in production bundle

## Notes

Prefer gating MSW behind `import.meta.dev` (or equivalent) rather than only removing the query-string check. Consider Nuxt route rules or conditional page exclusion for `__visual__`.
