---
name: prd-issues-tdd-local-main
description: Sequentially implement a PRD's local work items on the default branch using /tdd, with Docker Compose verification and Playwright visual snapshots for UI changes. Use when the user invokes /prd-issues-tdd-local-main, wants PRD items on main (not feature branches), compose-stack E2E checks, or golden tests for UI work.
user-invocable: true
---

# PRD Issues TDD — Main Branch + Compose + Visual

**Forge-first:** PRD and slices live as forge Discussions (see [/publish-docs](../publish-docs/SKILL.md)). Use `docs/prd/` mirror paths or `--items-path planning/...` as **read** sources. Comment on forge discussions when slices complete. Run `ogb docs pull` after status updates when available.

**Engineering contract:** [/engineering-contract](../engineering-contract/SKILL.md) — TDD, test layers, visual snapshots.

Like [prd-issues-tdd-local](../prd-issues-tdd-local/SKILL.md), but:

- **All work on the default branch** (usually `main`) — no per-issue feature branches.
- **Sensible commits** after each work item (or logical slice within one item).
- **Verify against the local Docker Compose stack** before marking an item done.
- **Playwright snapshot/golden tests** for every UI change so appearance is captured.
- **Done when every in-scope work item is implemented** — not when branches are merged.

## When to use

- PRD with multiple local work items and optional "blocked by" dependencies.
- Strict TDD via `/tdd`, one work item at a time.
- Local execution log instead of GitHub issues/PRs.
- User wants main-branch workflow with full stack verification and visual regression coverage.

## Inputs and arguments

When the user types `/prd-issues-tdd-local-main ...`, treat the rest as a single arguments string.

Parse arguments in this order:

1. **Root PRD file (required)** — path like `docs/PRD.md`, or a directory with a canonical PRD.
2. **`--items-path path/to/items.md`** (optional) — structured work-item source; overrides PRD extraction when clearer.
3. **`--ready-marker TEXT`** (optional, default: `ready for implementation`) — only include items with this marker that are not complete.
4. **`--default-branch BRANCH`** (optional) — working branch; default: detect from git (usually `main`).
5. **`--log-dir path/to/dir`** (optional, default: `planning/prd-issues-tdd-local-main/`).

Stop and ask if the PRD path or work items are ambiguous.

---

## Local artifacts

Under the log directory:

- `execution-plan.md` — ordered plan, dependency graph, branch strategy note (`main`)
- `progress-log.md` — sequential run log
- `items/<item-slug>.md` — per-work-item implementation record
- `items/<item-slug>-handoff.md` — `/tdd` handoff context

No `branches.md` — all work stays on the default branch.

---

## Phase 1 — Discover work items

Same as prd-issues-tdd-local Phase 1: read PRD, extract items (IDs, titles, bodies, ready/completion status), optionally merge `--items-path`, deduplicate.

---

## Phase 2 — Dependencies

Same as prd-issues-tdd-local Phase 2: parse "blocked by", build directed graph, warn on external deps, detect cycles. Write result to `execution-plan.md`.

---

## Phase 3 — Sequential execution order

Same as prd-issues-tdd-local Phase 3: topological sort, one subagent at a time, expose plan for user confirmation before starting.

Record in `execution-plan.md`:

```md
Branch strategy: **main** (all work items committed sequentially on default branch).
```

---

## Phase 4 — Main branch setup

1. Detect default branch (`--default-branch` or git remote HEAD).
2. `git fetch` and check out the default branch.
3. Pull if tracking a remote (confirm with user before force operations).
4. **Stay on this branch for the entire run.** Do not create feature branches.

---

## Phase 5 — Sequential `/tdd` execution per work item

For each work item in topological order:

### 5.1 Handoff

Save to `items/<item-slug>-handoff.md`:

- PRD path and relevant sections
- Work item ID, title, acceptance criteria
- Direct dependencies and full dependency chain
- Branch: `<default-branch>`

### 5.2 Invoke `/tdd`

Prompt the `/tdd` subagent with:

> Implement work item \<ID\> from PRD at \<path\> on branch \<default-branch\>.
>
> TDD loop: test → fail → minimal code → pass → repeat until acceptance criteria met.
>
> Do not start other work items. Keep changes scoped to this item.
>
> **UI changes:** add or extend Playwright visual snapshots (see Visual snapshot requirements below).
>
> **Backend/API changes:** add or extend unit/integration tests per project conventions.
>
> When green, commit on \<default-branch\> with a sensible message, then update the local implementation record.

### 5.3 Verify before marking done

Run **all** applicable checks. Do not advance until every check passes.

| Layer | When required | Typical commands |
|-------|---------------|------------------|
| Unit / handler tests | Always | `dotnet test` (scoped to touched projects when faster) |
| Web unit tests | UI or shared TS touched | `pnpm test` in `applications/opengitbase-web` |
| Compose stack | API, DB, migrations, or E2E scripts touched | See [compose-verification.md](compose-verification.md) |
| Domain E2E scripts | Feature has a `scripts/test-*-e2e.sh` | Run the relevant script against compose |
| Visual snapshots | Any UI component, page, or styling change | See Visual snapshot requirements |

If compose is not running, start it (`docker compose up -d --build`), wait for health, then run E2E scripts.

### 5.4 Commit discipline

- **One commit per work item** when the change set is cohesive; split only when backend and UI are independently reviewable.
- Message format: `<type>(<scope>): <what>` — e.g. `feat(discussions): anchored sub-thread replies`.
- Never commit broken tests or failing snapshots.

### 5.5 Implementation record

Update `items/<item-slug>.md`:

```md
## Summary
[What was built and how it was tested]

## Linked Context
- PRD: `docs/...`
- Work item: `<ID>`

## Dependency Graph
### Direct dependencies (blocked by)
- …

### Full chain
`<X> -> <Y> -> <ID>`

## Status
- Branch: `main`
- Tests: passing (list commands run)
- Visual snapshots: [paths or "none"]
- Commit(s): `<sha>`
```

Append to `progress-log.md`. Mark item completed in `execution-plan.md`.

---

## Visual snapshot requirements

**Required for every UI change** (new/changed components, pages, layouts, tokens affecting appearance).

1. **Gallery fixture** — add or extend a section in `applications/opengitbase-web/app/pages/__visual__/index.vue` with `data-testid="visual-<name>"` and representative props/fixtures (see existing `visual-discussion-sub-threads` pattern).
2. **Playwright spec** — add or extend `applications/opengitbase-web/tests/visual/<feature>.spec.ts`:
   - Use `waitForApp` helper (fonts ready, animations disabled).
   - `page.goto('/__visual__/?msw=1')` for gallery sections, or route-specific URLs with `?msw=1`.
   - `expect(...).toHaveScreenshot('name.png')` — Playwright generates mobile/tablet/desktop variants automatically.
3. **Generate baselines** — `cd applications/opengitbase-web && pnpm test:visual:update` (first run or intentional visual change).
4. **Verify** — `pnpm test:visual` must pass before the work item is done.
5. **Commit snapshots** — include `-snapshots/` PNGs in the same commit as the UI change.

For page-level flows (not gallery components), snapshot `body` or a stable `data-testid` region following existing specs in `tests/visual/shell.spec.ts` and `discussion-detail.spec.ts`.

---

## Phase 6 — Completion

**You are done when all in-scope work items are implemented** — every item is committed on the default branch, tests green, compose E2E passed where applicable, visual snapshots committed for UI work.

Final report:

1. Table: work item ID, title, status, commit SHA.
2. Verification summary: `dotnet test`, compose E2E scripts, `pnpm test`, `pnpm test:visual`.
3. Any skipped items (only with explicit user consent) and blocked dependents.

If a work item cannot be completed, pause and offer: adjust PRD, skip (not recommended if it blocks others), or stop.

---

## Safety

Confirm before: force-push, rebase, deleting branches, pushing to remote, marking skipped items complete.

Never spawn multiple `/tdd` subagents in parallel.

---

## Additional resources

- Compose stack and E2E scripts: [compose-verification.md](compose-verification.md)
