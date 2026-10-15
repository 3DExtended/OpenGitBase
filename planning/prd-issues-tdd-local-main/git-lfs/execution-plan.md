# Execution plan: Git LFS

**Source PRD:** [docs/prd/git-lfs.md](../../../docs/prd/git-lfs.md)  
**Work items:** [docs/issues/git-lfs/README.md](../../../docs/issues/git-lfs/README.md)  
**Parent forge:** [#234](https://api.opengitbase.com/opengitbase/open-git-base/discussions/234)

Branch strategy: **worktree branch `claude/lfs-implementation-difficulty-1a3fd2`** (all work items committed sequentially; `main` is checked out in the primary checkout, so the worktree branch stands in for it).

## Dependency order

| Order | ID | Title | Type | Status | Blocked by | Forge | Commit |
|-------|-----|-------|------|--------|------------|-------|--------|
| 1 | lfs-01 | Tracer bullet: git lfs push and clone round trip | AFK | done | — | #235 | 927eaf1 |
| 2 | lfs-02 | Permissions, size limits, batch cap and quota | AFK | done | lfs-01 | #236 | 7305f3e |
| 3 | lfs-03 | Encrypted quorum on verify | AFK | done | lfs-01 | #237 | f4d1e44 |
| 4 | lfs-04 | Pre-receive integrity gate for LFS pointers | AFK | done | lfs-01 | #238 | ea8e6da |
| 5 | lfs-05 | Reconciler repair from on-disk LFS inventory | AFK | done | lfs-03 | #239 | baa1dfd |
| 6 | lfs-06 | Cold recovery and post-promotion LFS restore | AFK | done | lfs-05 | #240 | 53e640e |
| 7 | lfs-07 | Web UI: LFS files in blob view and raw downloads | AFK | pending | lfs-01, lfs-02 | #241 | |
| 8 | lfs-08 | Repository deletion cleanup and admin LFS figures | AFK | pending | lfs-05 | #242 | |
| 9 | lfs-09 | LFS chaos E2E and production rollout | HITL | pending | lfs-03..lfs-08 | #243 | |

## Dependency graph

```
lfs-01 ─┬→ lfs-02 ──────────────┐
        ├→ lfs-03 → lfs-05 ─┬→ lfs-06 ─┐
        │                   └→ lfs-08 ─┤
        ├→ lfs-04 ─────────────────────┤
        └→ (lfs-02) → lfs-07 ──────────┴→ lfs-09 (HITL: production enable)
```

## Notes

- lfs-09's production deploy and `Lfs:Enabled` switch are human decisions; the run stops before them.
