# Execution Plan — Git HTTPS PAT

**PRD:** `docs/prd/git-https-personal-access-tokens.md`  
**Items:** `docs/issues/git-https-pat/`  
**Strategy:** Single branch `feat/git-https-pat`, sequential TDD, no parallelism  
**Default branch:** `main`

## Dependency graph

```
git-https-01 ──► git-https-02 ──► git-https-03 ──┐
                                                  ├──► git-https-04 ──► git-https-05 ──► git-https-08
git-https-01 ──► git-https-06 ◄── git-https-05   │
git-https-01,06 ─► git-https-07 ─────────────────┘
```

## Topological execution order

| Step | ID | Title | Status |
|------|-----|-------|--------|
| 1 | git-https-01 | Git access tokens + settings UI + git config | completed |
| 2 | git-https-02 | PAT repository access-check + storage HTTP routing | pending |
| 3 | git-https-03 | Storage git-http-backend | pending |
| 4 | git-https-04 | Dispatcher Smart HTTP edge | pending |
| 5 | git-https-05 | HAProxy unified HTTP routing + Cloudflare tunnel | pending |
| 6 | git-https-06 | SSH disable gate | pending |
| 7 | git-https-07 | Repository HTTPS clone URLs + settings navigation | pending |
| 8 | git-https-08 | End-to-end HTTPS git integration test | pending |

## Branching

All work items share **`feat/git-https-pat`** (branched from `main`). No per-item feature branches.
