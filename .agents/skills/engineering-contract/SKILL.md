---
name: engineering-contract
description: Agent engineering contract — read first. Covers TDD, test layers, forge-first docs, code structure pointers. Use at start of any implementation session or when onboarding to a project that references this skill.
---

# Engineering contract

Read this before writing code. Project-specific paths live in the repo's `.agents/` folder; skills live in [`.agents/skills/`](../skills/README.md).

## Read order (typical OpenGitBase session)

1. This skill
2. Repo `AGENTS.md`
3. Repo `.agents/state.md` and `.agents/code-structure.md`
4. Repo `.agents/testing.md` and `.agents/docs.md` when touching tests or specs
5. Invoke `/tdd` or `/prd-issues-tdd-local-main` for feature work

## TDD

**Default:** tracer-bullet TDD — one failing test → minimal code → green → refactor. Never horizontal "all tests then all code."

**With PRD/work item:** use `/tdd` inside `/prd-issues-tdd-local-main` (or `-local`). Do not advance to the next slice until acceptance criteria tests pass.

**Bug fixes:** add a regression test that reproduces the bug before fixing.

## Tests for everything

Every behavior change ships with the **lowest layer that proves the behavior**:

| Change touches | Required |
|----------------|----------|
| Domain / query handler | Unit test in feature `*.Tests` |
| API controller / auth | `OpenGitBase.Api.Tests` |
| CLI command | Stub HTTP + handler tests; JSON golden for stable `--json` |
| DB / migration / compose path | Compose E2E or tier script |
| Vue UI / styling | Playwright visual snapshots (see `/visual-snapshots`) |

Run applicable checks before marking work done. CI meta-tests enforce handler/controller/CLI coverage on OpenGitBase.

## Specs and planning (forge-first)

**Canonical:** Discussions on the forge via `ogb` (same repo as code for OpenGitBase).

**Not canonical:** authoring `docs/prd/*.md` directly — those paths are a **git mirror** exported by `ogb docs pull`.

Workflow: see [/publish-docs](../publish-docs/SKILL.md).

## Code structure

- **Deep modules** — small public interface, testable in isolation
- **Extend, don't duplicate** — follow existing patterns in the area you touch
- **CLI** — handlers → API client → output writers; no business logic in `CliApp`
- **Backend** — feature vertical slices under `features/`; use `agentGenCli new backend-feature`
- **Web** — Nuxt app under `applications/opengitbase-web/`

## Related skills

| Skill | When |
|-------|------|
| [tdd](../tdd/SKILL.md) | Any implementation |
| [publish-docs](../publish-docs/SKILL.md) | PRD, ADR, work-item slices |
| [to-prd-local](../to-prd-local/SKILL.md) | Create PRD discussion |
| [to-issues-local](../to-issues-local/SKILL.md) | Break PRD into linked slices |
| [prd-issues-tdd-local-main](../prd-issues-tdd-local-main/SKILL.md) | Implement slices on main |
| [visual-snapshots](../visual-snapshots/SKILL.md) | UI appearance changes |
| [cli-goldens](../cli-goldens/SKILL.md) | CLI output contracts |

## Tool split

| Tool | Use for |
|------|---------|
| `agentGenCli` | Scaffold, backend features, migrations, OpenAPI |
| `ogb` | Auth, discussions/issues, docs pull, MR — forge workflows |

Prerequisite: `ogb auth login` on machines that publish or pull specs.
