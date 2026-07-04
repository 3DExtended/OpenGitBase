# Anchor commit SHA links

## Metadata

- ID: cv-06
- Type: AFK
- Status: ready
- Source: docs/prd/commit-change-view.md

## Parent

[PRD: Commit Change View (Clickable Commits & Per-Commit Diff)](../../prd/commit-change-view.md)

## What to build

Wire commit SHA links into code-comment anchor previews for merge request review threads and repository discussions.

**Anchor header format (confirmed):**

- Display as `filePath:line` immediately followed by short SHA link — **no dot** between line number and SHA.
- Example visual: `src/auth.ts:42` `abc123de` (SHA is `RepoCommitLink`).

**Surfaces:**

- Discussion `CommentAnchorPreview` (sub-threads and replies).
- Merge request **Changes** tab review-comment anchor headers (via shared collaboration thread / anchor preview components).

**Context:**

- Discussion anchors pass `from=discussions/{number}`.
- MR review anchors pass `from=mr/{number}`.

File path portion may remain a link to blob browse at the anchored ref; SHA links to commit change view.

## Acceptance criteria

- [ ] Discussion anchored comment preview shows `path:line` + clickable short SHA (no dot separator)
- [ ] MR review thread anchor preview shows same format with `headCommitSha`
- [ ] Clicking SHA opens commit page with correct `from` back navigation
- [ ] Outdated anchor badges unchanged; file link behavior unchanged
- [ ] Anchors without stored commit SHA unchanged (no empty link)

## Blocked by

- [05-repocommitlink-mr-commits-tab.md](./05-repocommitlink-mr-commits-tab.md)

## User stories covered

- 24, 25, 27, 28

## Notes

- Prior art: `CommentAnchorPreview`, MR detail review thread rendering.
- Optional follow-up E2E: discussion anchor SHA click (not required for cv-06 acceptance if unit/visual coverage suffices).
