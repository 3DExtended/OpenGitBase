# Admin replication UI — execution plan

Source: `docs/prd/admin-replication-ui.md`  
Items: `docs/issues/admin-replication-ui/`

Branch: **main** (per user request)

## Order

| # | ID | Title | Blocked by |
|---|-----|-------|------------|
| 1 | admin-repl-01 | Repository replication list API | — |
| 2 | admin-repl-02 | Storage page fleet replication card | 01 |
| 3 | admin-repl-03 | Admin navigation and repository index | 01 |
| 4 | admin-repl-04 | Repository replication detail page | 01 |
| 5 | admin-repl-05 | Cross-surface polish and regression smoke | 02, 03, 04 |

## Dependency graph

```
01 → 02 ─┐
01 → 03 ─┼→ 05
01 → 04 ─┘
```
