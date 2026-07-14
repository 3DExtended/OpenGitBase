# Agent onboarding

This project was scaffolded with **agentGenCli** and has since grown into a distributed Git forge. Start here before making changes.

## Read order

1. [`.agents/skills/engineering-contract/SKILL.md`](.agents/skills/engineering-contract/SKILL.md) — TDD, tests, forge-first docs
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

All skills live under [`.agents/skills/`](.agents/skills/README.md).

| Invoke | Skill path | Purpose |
|--------|------------|---------|
| `/engineering-contract` | [engineering-contract/SKILL.md](.agents/skills/engineering-contract/SKILL.md) | Non-negotiables (read first) |
| `/to-prd-local` | [to-prd-local/SKILL.md](.agents/skills/to-prd-local/SKILL.md) | Publish PRD discussion |
| `/to-issues-local` | [to-issues-local/SKILL.md](.agents/skills/to-issues-local/SKILL.md) | Publish linked slice discussions |
| `/prd-issues-tdd-local-main` | [prd-issues-tdd-local-main/SKILL.md](.agents/skills/prd-issues-tdd-local-main/SKILL.md) | Implement slices on `main` with TDD |
| `/tdd` | [tdd/SKILL.md](.agents/skills/tdd/SKILL.md) | Tracer-bullet test loop |
| `/publish-docs` | [publish-docs/SKILL.md](.agents/skills/publish-docs/SKILL.md) | Forge publish + mirror sync |
| `/cli-goldens` | [cli-goldens/SKILL.md](.agents/skills/cli-goldens/SKILL.md) | CLI output contracts |
| `/visual-snapshots` | [visual-snapshots/SKILL.md](.agents/skills/visual-snapshots/SKILL.md) | Playwright UI regression |

Prefer `agentGenCli new …` and `agentGenCli project …` over hand-rolling feature structure.

Project manifest: [`.agentGenCli.json`](.agentGenCli.json)
