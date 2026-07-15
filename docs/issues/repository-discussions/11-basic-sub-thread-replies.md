<!-- forge: #196 -->

# Basic sub-thread replies

## Metadata

- ID: disc-11
- Type: AFK
- Status: ready
- Source: docs/prd/discussion-sub-threads.md

## Parent

[PRD: Discussion Sub-Threads (Replies and Per-Comment Resolution)](../../prd/discussion-sub-threads.md)

## What to build

**One-level sub-thread replies** on discussion comments: persistence, API, lifecycle differentiation, and web UI for nested display.

**Data model:** add optional `parentCommentId` on `discussion_comments`. `null` = top-level (sub-thread root); non-null = reply to a root only.

**Comment create (reply):**
- Authenticated Reader+ (not blocked).
- Accept optional `parentCommentId` referencing a visible root comment in the same discussion.
- Reject reply when parent is itself a reply (reply-to-reply → 400).
- Reject reply when parent root is soft-deleted (deferred enforcement until disc-14; may return 400 once orphan rules ship).
- Same Markdown safety posture as existing comments.

**Lifecycle hooks on reply create:**
- Replies **never** trigger Open → Engaged.
- Reply on **Resolved** or **Dismissed** discussion → reopen to **Open** (never Engaged), same as top-level reopen.
- Top-level comment rules unchanged: first non-creator top-level comment Engages once; top-level comment on closed discussion reopens.

**List comments:**
- `GET` returns top-level comments ordered by `createdAt` ascending.
- Each root includes nested `replies[]` ordered by `createdAt` ascending.
- Root projection includes `replyCount` and `lastReplyAt` for UI headers.

**UI:**
- Discussion detail: top-level comments render as sub-thread roots with nested replies underneath.
- Per-root **Reply** action and inline reply composer.
- Bottom sticky composer remains for **new top-level comments** only.
- Edit and soft-delete on replies use existing comment rules.

## Acceptance criteria

- [ ] Migration adds `parent_comment_id` (nullable FK to same table)
- [ ] POST comment with `parentCommentId` creates reply under root; appears in `replies[]`
- [ ] POST reply with `parentCommentId` pointing at a reply → 400
- [ ] GET comments returns roots with nested `replies[]` in chronological order
- [ ] Top-level comment by non-creator still triggers Engaged once
- [ ] Reply by non-creator does not trigger Engaged
- [ ] Reply on Resolved/Dismissed discussion reopens to Open; does not Engage
- [ ] Author edit and soft-delete work on replies
- [ ] Writer+ soft-delete works on replies
- [ ] Blocked user cannot post reply (when disc-06 deployed)
- [ ] Web UI: nested render, per-root reply composer, bottom composer for top-level only
- [ ] API tests for nesting, validation, and lifecycle matrix

## Blocked by

- [04-thread-comments-engagement-lifecycle.md](./04-thread-comments-engagement-lifecycle.md)
- [09-anchored-code-comments.md](./09-anchored-code-comments.md)

## User stories covered

- 1, 2, 3, 4, 5
- 21, 22, 23, 24
- 30, 31, 32, 38
- 39, 40, 41, 42
- 46 (partial)

## Notes

- Anchored replies deferred to disc-12; this slice is markdown-only replies.
- Sub-thread resolve/collapse deferred to disc-13.
- Orphan reply promotion deferred to disc-14.
- @mentions in replies follow disc-07 when available; store raw `@username` in body until then.
