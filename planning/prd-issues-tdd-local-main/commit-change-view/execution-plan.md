# Execution plan — Commit Change View

**PRD:** `docs/prd/commit-change-view.md`  
**Work items:** `docs/issues/commit-change-view/`  
**Branch strategy:** **main** (all work items committed sequentially on default branch).

## Ordered work items

| Order | ID | Title | Type | Status | Blocked by |
|-------|-----|-------|------|--------|------------|
| 1 | cv-01 | Storage commit read helpers | AFK | pending | git-storage-proxy |
| 2 | cv-02 | Commit read API | AFK | pending | cv-01 |
| 3 | cv-03 | Shared unified diff viewer | AFK | pending | — (parallel with cv-02) |
| 4 | cv-04 | Commit page shell | AFK | pending | cv-02, cv-03 |
| 5 | cv-05 | RepoCommitLink and MR Commits tab | AFK | pending | cv-04 |
| 6 | cv-06 | Anchor commit SHA links | AFK | pending | cv-05 |
| 7 | cv-07 | Ref picker commit SHA chip | AFK | pending | cv-05 |

## Dependency graph

```
git-storage-proxy ──► cv-01 ──► cv-02 ──► cv-04 ──► cv-05 ──► cv-06
                              cv-03 ──► cv-04              └──► cv-07
```

**First demo milestone:** cv-05 — MR Commits tab click-through to commit page.

## Execution order (topological)

1. cv-01
2. cv-03 (can interleave with cv-02 after cv-01)
3. cv-02
4. cv-04
5. cv-05
6. cv-06
7. cv-07
