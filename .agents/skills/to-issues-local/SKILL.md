---
name: to-issues-local
description: Break a plan or PRD into forge Discussion work-item slices with links (forge-first), then sync git mirror. Use for tracer-bullet work items — not local-only planning files.
---

# To issues (forge-first)

Work items are **Discussions** on the forge, linked to the parent PRD discussion. Git `docs/issues/` / `planning/` is a mirror via `ogb docs pull`.

Read [/publish-docs](../publish-docs/SKILL.md) first.

## Process

### 1. Gather context

From conversation or PRD discussion number / `docs/prd/` mirror path. If parent is a forge discussion, `ogb issue view N` for full body.

### 2. Explore codebase (optional)

Use domain glossary and ADRs.

### 3. Draft vertical slices

Tracer bullets — thin end-to-end slices, not horizontal layers. Prefer AFK over HITL.

### 4. Quiz the user

Present breakdown: title, type, blocked-by, user stories. Iterate until approved.

### 5. Publish slices (dependency order)

For each approved slice:

```bash
ogb issue create --title "[slice] id — Title" --body-file slice.md
ogb issue link <child> --parent <prd-number>
```

Record all discussion numbers.

### 6. Sync mirror

```bash
ogb docs pull
git add docs/issues planning && git commit -m "docs: sync slices #43–#50"
```

### 7. Index

Maintain forge numbers as primary IDs. Mirror index lists: id, title, type, status, blocked-by, forge `#N`.

<issue-template>
# [slice] id — Title

## Metadata

- Forge: #N
- Type: AFK | HITL
- Status: ready

## Parent

PRD discussion #42

## What to build

End-to-end vertical slice description.

## Acceptance criteria

- [ ] …

## Blocked by

- #43 or None

## User stories covered

- …

</issue-template>

Do not close parent PRD unless asked.
