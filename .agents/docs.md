# Docs and planning — OpenGitBase

**Forge-first:** PRDs, ADRs, and work-item slices are **Discussions** in this repository on the running forge. Git paths under `docs/` are a **mirror**, not the authoring surface.

Read global skills: **`publish-docs`**, **`engineering-contract`**.

## Prerequisites

```bash
ogb auth login
ogb auth status
```

Default host: production or `--hostname http://localhost:8089` for local compose.

Repository: **same repo as code** (`-R owner/open-git-base` or infer from git `origin`).

## Title conventions

| Kind | Example title |
|------|----------------|
| PRD | `[PRD] ogb mr — Merge Request CLI` |
| ADR | `[ADR] 0005 — Discussion-to-discussion links` |
| Slice | `[slice] mr-01 — API client and mr list` |

Optional tags on discussions when supported: `prd`, `adr`, `slice`.

## Workflow

1. **Create** — `ogb issue create --title "[PRD] …" --body-file draft.md`
2. **Link slices** — `ogb issue link` (planned) or `#N` references in bodies
3. **Update** — `ogb issue comment N --body-file update.md`
4. **Mirror** — `ogb docs pull` → commit `docs/prd/`, `docs/adr/`, `docs/issues/`

## Bootstrap fallback (until `ogb docs pull` ships)

When publishing, also write the mirror file and reference forge id in metadata:

```markdown
<!-- forge: #42 -->
```

Commit: `docs: sync PRD discussion #42 (bootstrap)`.

Remove bootstrap writes once `ogb docs pull` is implemented ([PRD](../docs/prd/ogb-docs-and-discussion-links.md)).

## Paths (mirror)

| Mirror path | Content |
|-------------|---------|
| `docs/prd/` | Product specs |
| `docs/adr/` | Architecture decisions |
| `docs/issues/` | Exported slice bodies (optional) |
| `planning/` | Execution logs from `/prd-issues-tdd-local-main` |

## Tool split

| Tool | Use |
|------|-----|
| `ogb` | Publish/update/pull specs, issues, MR |
| `agentGenCli` | Code scaffold only — not spec publish |

## Link types (target)

- PRD → slice: `parent` / `child`
- Slice → slice: `blocks`
- Cross-ref: `related`

Implementation: [ogb-docs-and-discussion-links PRD](../docs/prd/ogb-docs-and-discussion-links.md).

## Skills

- `/to-prd-local` — publish PRD discussion
- `/to-issues-local` — publish linked slices
- `/prd-issues-tdd-local-main` — implement on main

Never skip forge publish for planning artifacts.
