# PRD issues TDD — execution plan

Source: [docs/prd/discussion-sub-threads.md](../../docs/prd/discussion-sub-threads.md)

Branch strategy: **main** (per user request; no per-issue feature branches).

| Order | ID | Title | Status |
|------:|-----|-------|--------|
| 1 | disc-11 | Basic sub-thread replies | completed |
| 2 | disc-12 | Anchored replies | completed |
| 3 | disc-13 | Sub-thread resolve and collapse UI | completed |
| 4 | disc-14 | Orphan replies after root soft-delete | completed |
| 5 | disc-15 | Sub-thread resolve notifications | completed |
| 6 | disc-16 | Sub-thread integration tests | completed |

## Dependency graph

```
disc-04, disc-09 (pre-existing) ──► disc-11 ──┬──► disc-12
                                            ├──► disc-13 ──► disc-15
                                            └──► disc-14
disc-11…disc-15 ──► disc-16
```

## Verification

- `dotnet test tests/OpenGitBase.Features.Discussion.Tests` — 57 passed
- `./scripts/test-discussions-e2e.sh` against Docker Compose — passed (includes sub-thread scenarios)
- `npm test` in `opengitbase-web` — 19 passed
- Playwright `discussion-subthreads.spec.ts` — 3 viewport snapshots generated
