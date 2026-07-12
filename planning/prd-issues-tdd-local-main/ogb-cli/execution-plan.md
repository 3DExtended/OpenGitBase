# Execution plan — `ogb` CLI

**Source PRD:** [docs/prd/ogb-cli.md](../../../docs/prd/ogb-cli.md)  
**Work items:** [planning/ogb-cli/index.md](../../../planning/ogb-cli/index.md)

Branch strategy: **main** (all work items committed sequentially on default branch).

## Topological order

| # | ID | Title | Blocked by |
|---|-----|-------|------------|
| 1 | cli-01 | CLI project bootstrap | — |
| 2 | cli-02 | Host resolver and config store | cli-01 |
| 3 | cli-03 | Nuxt `/cli/auth` page | cli-01 |
| 4 | cli-04 | Loopback listener and `ogb auth login` | cli-02, cli-03 |
| 5 | cli-05 | `ogb auth status` and `ogb auth logout` | cli-04 |
| 6 | cli-06 | OS keychain credential storage | cli-04 |
| 7 | cli-07 | Repo context (`-R` / git remote) | cli-04 |
| 8 | cli-08 | `ogb issue create` | cli-07 |
| 9 | cli-09 | `ogb issue comment` | cli-08 |
| 10 | cli-10 | `ogb issue close` | cli-08 |
| 11 | cli-11 | `ogb issue list` | cli-08 |
| 12 | cli-12 | `ogb issue view` | cli-08 |
| 13 | cli-13 | `ogb issue status` | cli-08 |
| 14 | cli-14 | `--json` output | cli-05, cli-13 |
| 15 | cli-15 | Exit codes and structured errors | cli-14 |

## Dependency graph

```
cli-01 ──┬──► cli-02 ──► cli-04 ──┬──► cli-05 ──► cli-14 ──► cli-15
         │                        ├──► cli-06
         └──► cli-03 ─────────────┘
                                  └──► cli-07 ──► cli-08 ──┬──► cli-09…cli-13 ──► cli-14
```

## Status

| ID | Status | Commit |
|----|--------|--------|
| cli-01 | in_progress | — |
| cli-02 | pending | — |
| cli-03 | pending | — |
| cli-04 | pending | — |
| cli-05 | pending | — |
| cli-06 | pending | — |
| cli-07 | pending | — |
| cli-08 | pending | — |
| cli-09 | pending | — |
| cli-10 | pending | — |
| cli-11 | pending | — |
| cli-12 | pending | — |
| cli-13 | pending | — |
| cli-14 | pending | — |
| cli-15 | pending | — |
