# Agent onboarding

This project was scaffolded with **agentGenCli** and has since grown into a distributed Git forge. Start here before making changes.

## Read order

1. Global skill **`engineering-contract`** — TDD, tests, forge-first docs (portable)
2. [`docs/PROJECT-STATE.md`](docs/PROJECT-STATE.md) — implementation baseline, components, encryption
3. [`.agents/state.md`](.agents/state.md) — enabled stacks and features
4. [`.agents/code-structure.md`](.agents/code-structure.md) — where code lives, how to extend
5. [`.agents/testing.md`](.agents/testing.md) — test layers, compose, meta-tests
6. [`.agents/docs.md`](.agents/docs.md) — PRD/ADR/slices on forge via `ogb`
7. [`.agents/README.md`](.agents/README.md) — safe-change rules
8. [`.agents/backend.md`](.agents/backend.md) — when touching .NET

## Before spec or forge work

```bash
ogb auth login
```

## Common skills

| Invoke | Purpose |
|--------|---------|
| `/engineering-contract` | Non-negotiables (read first) |
| `/to-prd-local` | Publish PRD discussion |
| `/to-issues-local` | Publish linked slice discussions |
| `/prd-issues-tdd-local-main` | Implement slices on `main` with TDD |
| `/tdd` | Tracer-bullet test loop |
| `/publish-docs` | Forge publish + mirror sync |

Prefer `agentGenCli new …` and `agentGenCli project …` over hand-rolling feature structure.

Project manifest: [`.agentGenCli.json`](.agentGenCli.json)
