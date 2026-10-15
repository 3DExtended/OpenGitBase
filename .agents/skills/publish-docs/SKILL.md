---
name: publish-docs
description: Forge-first docs workflow — publish PRDs, ADRs, and work-item slices as linked Discussions via ogb, then pull git mirror. Use when creating or updating specs, not for local-only markdown authoring.
---

# Publish docs (forge-first)

Discussions on the forge are **canonical**. Git paths `docs/prd/`, `docs/adr/`, `docs/issues/` are a **mirror** for search, review, and offline agents.

## Prerequisites

- `ogb auth login` against the target host
- Repository context: `-R owner/slug` or git `origin` in clone
- OpenGitBase planning uses the **same repo as code** (e.g. `opengitbase/open-git-base`)

## Title conventions

| Kind | Title pattern | Tag (optional) |
|------|---------------|----------------|
| PRD | `[PRD] Feature name` | `prd` |
| ADR | `[ADR] NNNN — Decision title` | `adr` |
| Work slice | `[slice] id — Short title` | `slice` |

## Create PRD

```bash
ogb issue create --title "[PRD] Feature name" --body-file /path/to/draft.md
# Note discussion number from output, e.g. #42
```

Draft the body locally in a temp file if needed; **publish is the write**. Do not treat `docs/prd/` as the authoring surface.

Bodies over 8000 characters are split by `ogb issue create` into a body plus continuation comments (≤ 16000 each, cut at blank lines outside code fences) and posted immediately — never split by hand.

## Create ADR

Same as PRD with `[ADR] NNNN — …` title. Link to related PRDs when applicable.

## Create work-item slices

For each tracer-bullet slice:

```bash
ogb issue create --title "[slice] mr-01 — API client" --body-file slice.md
ogb issue link 43 --parent 42
```

## Link types

- `parent` / `child` — PRD ↔ slice
- `related` — cross-reference
- `blocks` — dependency

## Sync git mirror

After publish or update:

```bash
ogb docs pull
git add docs/prd docs/adr docs/issues
git commit -m "docs: sync discussions #42–#48"
```

## What gets mirrored

`ogb docs pull` writes, after `<!-- forge: #N -->`:

1. The discussion body.
2. The creator's top-level comments from the **publish burst** — each posted within 10 minutes of the previous post; the burst ends at the first longer gap or the first comment by someone else.

Parts are joined by one blank line. The API copies the body into the opening comment; that copy replaces the body when it matches or has been edited, so it never appears twice.

Comments posted later (audits, status/closing notes, `ogb issue comment` updates), other authors' comments, and sub-thread replies stay **forge-only**.

The pull updates the file whose first `<!-- forge: #N -->` marker matches, anywhere under `docs/prd`, `docs/adr`, or `docs/issues`. Only discussions without such a file get a new slug path from the title (with a `-N` suffix if that path is taken).

## Update existing spec

Edit the spec's opening or continuation comment on the forge (web UI), then:

```bash
ogb docs pull && git commit -m "docs: sync discussion #42"
```

`ogb issue comment 42 --body-file note.md` adds a discussion comment (status, audit, follow-up). It does **not** change the mirrored spec.

## Skills that use this workflow

- `/to-prd-local` — creates PRD discussion (not local file first)
- `/to-issues-local` — creates linked slice discussions
- `/prd-issues-tdd-local-main` — implements slices; comment on discussion when done

## Failure modes

| Situation | Action |
|-----------|--------|
| Not logged in | Run `ogb auth login`; do not fall back to local-only |
| Pull fails | Publish still counts; retry pull; warn user |
| Pull writes `name-N.md` | Another file holds the slug path — usually a duplicate discussion; dismiss or retitle it on the forge |
| Mirror lacks a late update | It was posted as a new comment; edit the spec comment instead and pull again |
