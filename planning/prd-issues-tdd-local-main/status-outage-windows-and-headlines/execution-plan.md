# Execution plan: Status outage windows and headlines

**Source PRD:** [docs/prd/status-outage-windows-and-headlines.md](../../../docs/prd/status-outage-windows-and-headlines.md)  
**Work items:** [docs/issues/status-outage-windows-and-headlines/README.md](../../../docs/issues/status-outage-windows-and-headlines/README.md)  
**Parent forge:** [#220](https://api.opengitbase.com/opengitbase/open-git-base/discussions/220)

Branch strategy: **main** (all work items committed sequentially on default branch).

## Dependency order

| Order | ID | Title | Type | Status | Blocked by | Forge | Commit |
|-------|-----|-------|------|--------|------------|-------|--------|
| 1 | sow-01 | Detector, store, openWindows on snapshot | AFK | completed | — | #221 | `0eefe78` |
| 2 | sow-02 | Public windows history API | AFK | completed | sow-01 | #222 | `56dd92e` |
| 3 | sow-04 | Admin suppress and annotate windows | AFK | completed | sow-01 | #223 | `48403d3` |
| 4 | sow-03 | Public status outage timeline UI | AFK | completed | sow-02 | #224 | `44c3faa` |
| 5 | sow-05 | Retention prune and 90-day archive | AFK | completed | sow-03, sow-04 | #225 | `a54aa2b` |

## Dependency graph

```
sow-01 → sow-02 → sow-03 ─┐
       └→ sow-04 ─────────┴→ sow-05
```

## Notes

- Compose verification skipped (Docker daemon unavailable); verification via `dotnet test`, vitest, vue-tsc, Playwright.
- Unrelated pre-existing visual flakiness left untouched.
