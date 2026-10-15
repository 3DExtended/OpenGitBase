# Docs and planning — OpenGitBase

**Forge-first:** PRDs, ADRs, and work-item slices are **Discussions** in this repository on the running forge. Git paths under `docs/` are a **mirror**, not the authoring surface.

Read skills in [`.agents/skills/`](skills/README.md): **`publish-docs`**, **`engineering-contract`**.

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

1. **Create** — `ogb issue create --title "[PRD] …" --body-file draft.md` (long drafts are split automatically, see below)
2. **Link slices** — `ogb issue link 43 --parent 42` or `#N` references in bodies
3. **Update the spec** — `ogb issue edit N --body-file spec.md` with the full new text (see *Rewriting a spec* below). A *new* comment (`ogb issue comment N`) is discussion — status notes, audits, follow-ups — and stays forge-only.
4. **Mirror** — `ogb docs pull` → commit `docs/prd/`, `docs/adr/`, `docs/issues/`

## What a mirror file contains

Discussion bodies are capped at 8000 characters and comments at 16000, so long specs are published as body + continuation comments. A mirror file holds:

1. The discussion body.
2. The creator's top-level comments from the **publish burst**: in order, each posted within 10 minutes of the previous post. The burst ends at the first longer gap or the first comment by someone else.
3. Parts joined by one blank line, after `<!-- forge: #N -->`.

The API copies the body into the opening comment. Only comments are editable, so that copy replaces the body when it matches or has been edited; it never appears twice.

**Forge-only (never mirrored):** comments posted after the burst (implementation audits, status and closing notes, `ogb issue comment` updates), comments by anyone other than the creator, and sub-thread replies.

**Long drafts:** `ogb issue create` splits a body over 8000 characters into a body ≤ 8000 plus continuation comments ≤ 16000, cut at blank lines outside code fences, and posts them immediately. Do not split by hand.

## Rewriting a spec

`ogb issue edit N --body-file spec.md` replaces the spec with the full text of `spec.md`, keeping its parts inside the publish burst:

- The text is split the same way, except that the first part goes into the opening comment, which holds up to 16000 characters like any comment. A one-part spec can grow to 16000 in place.
- Parts are edited in order; parts whose text is unchanged are skipped, so re-running after a failure finishes the job.
- If the new text needs **fewer** parts, the leftover continuation comments are deleted so the mirror drops them.
- It refuses, before changing anything, text that needs **more** parts than the spec has (a comment posted now falls outside the burst and is never mirrored), specs you did not publish, and old specs whose body has no opening comment. For those, shorten the text, or publish a new issue and dismiss the old one.

To fix one comment — a typo in a status note, say — use `ogb issue comment-edit <comment-id> --body-file note.md`. Comment ids are in `ogb issue view N --json`; only a comment's author can edit it.

The web UI's card at the top of a discussion shows the original body, which the API cannot change. After an edit, the opening comment below it holds the current text.

## Where pulled files land

- `ogb docs pull` updates the file under the kind folder (`docs/prd`, `docs/adr`, `docs/issues`, any depth) whose first `<!-- forge: #N -->` marker matches the discussion. Renaming or moving a mirror file is safe while the marker stays.
- Otherwise it writes the slug path from the title (`[PRD] Foo` → `docs/prd/foo.md`, `[slice] mr-04 — …` → `docs/issues/mr-04.md`). If another file already holds that path, the name gets a `-N` suffix (`foo-213.md`) — usually a duplicate discussion worth dismissing or retitling.
- Files are UTF-8 without BOM.

## Bootstrap fallback (legacy)

Before `ogb docs pull`, agents dual-wrote mirror files during publish. Prefer pull now:

```bash
ogb docs pull
```

When hand-editing mirror files, keep forge metadata:

```markdown
<!-- forge: #42 -->
```

Commit: `docs: sync PRD discussion #42`.

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

See [`.agents/skills/README.md`](skills/README.md):

- `/to-prd-local` — publish PRD discussion
- `/to-issues-local` — publish linked slices
- `/prd-issues-tdd-local-main` — implement on main

Never skip forge publish for planning artifacts.
