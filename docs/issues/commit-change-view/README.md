# Commit Change View — implementation issues

Vertical slices for [PRD: Commit Change View (Clickable Commits & Per-Commit Diff)](../../prd/commit-change-view.md).

Implement in dependency order on a feature branch (e.g. `feat/commit-change-view`); each issue is blocked by the ones listed in its file.

| # | ID | Issue | Type | Status | Blocked by |
|---|-----|-------|------|--------|------------|
| 1 | `cv-01` | [Storage commit read helpers](./01-storage-commit-read-helpers.md) | AFK | ready | [git-storage-proxy](../git-storage-proxy/README.md) |
| 2 | `cv-02` | [Commit read API](./02-commit-read-api.md) | AFK | ready | 1 |
| 3 | `cv-03` | [Shared unified diff viewer](./03-shared-unified-diff-viewer.md) | AFK | ready | — |
| 4 | `cv-04` | [Commit page shell](./04-commit-page-shell.md) | AFK | ready | 2, 3 |
| 5 | `cv-05` | [RepoCommitLink and MR Commits tab](./05-repocommitlink-mr-commits-tab.md) | AFK | ready | 4 |
| 6 | `cv-06` | [Anchor commit SHA links](./06-anchor-commit-sha-links.md) | AFK | ready | 5 |
| 7 | `cv-07` | [Ref picker commit SHA chip](./07-ref-picker-commit-sha-chip.md) | AFK | ready | 5 |

**First demo milestone:** complete **cv-05** — open a merge request **Commits** tab, click a commit card, land on the commit page with the correct first-parent diff, and navigate back to `!n`.

## Dependency graph

```
git-storage-proxy ──► cv-01 ──► cv-02 ──► cv-04 ──► cv-05 ──► cv-06
                              cv-03 ──► cv-04              └──► cv-07
```

**cv-03** can start in parallel with **cv-02** (MR Changes tab already exists).

## Source

- [docs/prd/commit-change-view.md](../../prd/commit-change-view.md)
