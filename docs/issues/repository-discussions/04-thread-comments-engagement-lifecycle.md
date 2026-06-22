# Thread comments and engagement lifecycle

## Metadata

- ID: disc-04
- Type: AFK
- Status: ready
- Source: docs/prd/repository-discussions.md

## Parent

[PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md)

## What to build

**Thread comments** on discussions: persistence, API, Markdown rendering, moderation, and lifecycle hooks.

**Data model:** `discussion_comments` with `body` (Markdown source), rendered `bodyHtml`, author, timestamps, `editedAt`. Soft-delete only — `deletedAt`, `deletedByUserId`; **no hard deletes** at DB layer.

**Comment create:**
- Authenticated Reader+ (not blocked).
- Markdown input; sanitize rendered HTML; disallow raw HTML in source (same posture as README).
- Flat chronological list (no nested replies in v1).

**Lifecycle hooks on comment create:**
- First comment by **non-creator** while Open and `hasEverBeenEngaged == false` → status **Engaged**; set `hasEverBeenEngaged`.
- Comment on **Resolved** or **Dismissed** → reopen to **Open** (never Engaged), regardless of author.
- After `hasEverBeenEngaged`, non-creator comments do **not** re-Engage.

**Edit / delete:**
- Author may edit anytime; show “edited” in UI.
- Author may soft-delete own comment.
- Writer+ may soft-delete any comment (moderation).
- Soft-deleted comments hidden from default thread view.

**UI:** Comment composer and thread on discussion detail page.

## Acceptance criteria

- [ ] Create comment on Open discussion; appears in chronological thread
- [ ] First non-creator comment transitions Open → Engaged once
- [ ] Second non-creator comment on same Open discussion does not re-Engage after `hasEverBeenEngaged`
- [ ] Comment on Resolved/Dismissed reopens to Open
- [ ] Reopened discussion: non-creator comment stays Open (does not become Engaged)
- [ ] Markdown formatting renders; script injection via comment blocked
- [ ] Author edit updates body and sets edited indicator
- [ ] Author soft-delete hides comment from UI; row retained in DB
- [ ] Writer+ soft-delete hides any comment
- [ ] Blocked user cannot comment (when disc-06 deployed)
- [ ] API and UI tests for engagement and reopen scenarios

## Blocked by

- [03-discussion-detail-assignee-writer-close.md](./03-discussion-detail-assignee-writer-close.md)

## User stories covered

- 15, 16, 20, 21, 25, 26, 27, 28, 29, 30, 31, 32, 60, 62

## Notes

- @mentions and auto-subscribe deferred to disc-07; store raw `@username` text in body for now.
- Pagination deferred; load full thread in v1.
