# `ogb` CLI — work items

**Source PRD:** [docs/prd/ogb-cli.md](../../docs/prd/ogb-cli.md)

Vertical slices for the OpenGitBase forge CLI (`ogb`): loopback authentication and issue-style commands mapped to the Discussion API.

## Work items

| Order | ID | Title | Type | Status | Blocked by |
|-------|-----|-------|------|--------|------------|
| 1 | [cli-01](./items/cli-01.md) | CLI project bootstrap | AFK | ready | — |
| 2 | [cli-02](./items/cli-02.md) | Host resolver and config store | AFK | ready | cli-01 |
| 3 | [cli-03](./items/cli-03.md) | Nuxt `/cli/auth` page | AFK | ready | cli-01 |
| 4 | [cli-04](./items/cli-04.md) | Loopback listener and `ogb auth login` | AFK | ready | cli-02, cli-03 |
| 5 | [cli-05](./items/cli-05.md) | `ogb auth status` and `ogb auth logout` | AFK | ready | cli-04 |
| 6 | [cli-06](./items/cli-06.md) | OS keychain credential storage | AFK | ready | cli-04 |
| 7 | [cli-07](./items/cli-07.md) | Repo context (`-R` / git remote inference) | AFK | ready | cli-04 |
| 8 | [cli-08](./items/cli-08.md) | `ogb issue create` | AFK | ready | cli-07 |
| 9 | [cli-09](./items/cli-09.md) | `ogb issue comment` | AFK | ready | cli-08 |
| 10 | [cli-10](./items/cli-10.md) | `ogb issue close` | AFK | ready | cli-08 |
| 11 | [cli-11](./items/cli-11.md) | `ogb issue list` | AFK | ready | cli-08 |
| 12 | [cli-12](./items/cli-12.md) | `ogb issue view` | AFK | ready | cli-08 |
| 13 | [cli-13](./items/cli-13.md) | `ogb issue status` | AFK | ready | cli-08 |
| 14 | [cli-14](./items/cli-14.md) | `--json` output | AFK | ready | cli-05, cli-13 |
| 15 | [cli-15](./items/cli-15.md) | Exit codes and structured errors | AFK | ready | cli-14 |

## Dependency graph

```
cli-01 ──┬──► cli-02 ──► cli-04 ──┬──► cli-05 ──► cli-14 ──► cli-15
         │                        ├──► cli-06
         └──► cli-03 ─────────────┘
                                  └──► cli-07 ──► cli-08 ──┬──► cli-09
                                                           ├──► cli-10
                                                           ├──► cli-11
                                                           ├──► cli-12
                                                           └──► cli-13 ──► cli-14
```

## Parallelism

After **cli-08** completes, **cli-09** through **cli-13** may proceed in parallel.

**cli-06** (keychain) may proceed in parallel with **cli-05** and **cli-07** once **cli-04** is done.

## Verification (each item)

- **cli-01–cli-02, cli-04–cli-15:** `dotnet test` for CLI project tests
- **cli-03:** `pnpm test` (Nuxt route/component test for `/cli/auth`)
- **cli-04+:** manual smoke — `ogb auth login` against local compose stack (document in item notes)

## Out of scope (deferred)

- CI/CD pipeline tokens (`OGB_TOKEN`) — separate follow-up PRD
- OAuth provider buttons on CLI auth page
- Merge request commands, git operations, discussion tags/assignee/anchors
