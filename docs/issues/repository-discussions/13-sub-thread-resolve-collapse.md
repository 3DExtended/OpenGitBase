# Sub-thread resolve and collapse UI

## Metadata

- ID: disc-13
- Type: AFK
- Status: ready
- Source: docs/prd/discussion-sub-threads.md

## Parent

[PRD: Discussion Sub-Threads (Replies and Per-Comment Resolution)](../../prd/discussion-sub-threads.md)

## What to build

**Per-root sub-thread resolution**: visual-only resolve state, API, and collapsed presentation on the discussion detail page.

**Data model:** on root comments only (`parentCommentId == null`):
- `resolvedAt` (nullable timestamp)
- `resolvedByUserId` (nullable user FK)

**Resolve / unresolve API:**
- `POST …/comments/{commentId}/resolve` — root author or Writer+.
- `POST …/comments/{commentId}/unresolve` — same permissions.
- Only root comments may be resolved; resolving a reply → 400.
- Idempotent resolve and unresolve (200 on repeat).
- Discussion `status` unchanged by sub-thread resolve/unresolve.
- Bump discussion `updatedAt` on resolve/unresolve so list sort reflects triage activity.

**Resolution semantics (visual only):**
- Resolved roots show `isResolved` (derived from `resolvedAt`) in list DTO.
- Replies remain permitted on resolved sub-threads; resolve does not lock the branch.

**UI:**
- Root header: **Resolve** / **Unresolve** toggle for permitted users.
- Resolved badge on resolved roots.
- Resolved sub-threads **collapsed by default**; manual expand/collapse toggle on header.
- Collapsed header shows reply count and/or last activity hint.
- Posting a reply on a collapsed resolved sub-thread **auto-expands** the thread; resolved badge **remains** until explicit unresolve.
- Strict chronological root order unchanged; resolved threads collapse in place (not moved to a separate section).

## Acceptance criteria

- [ ] Migration adds `resolved_at` and `resolved_by_user_id` on discussion comments
- [ ] Root author can resolve own sub-thread; other Reader cannot resolve stranger's root
- [ ] Writer+ can resolve any sub-thread
- [ ] Resolve/unresolve on a reply → 400
- [ ] Discussion status unchanged after resolve; POST reply on resolved sub-thread still succeeds
- [ ] Resolved badge persists after new reply until unresolve
- [ ] Discussion `updatedAt` bumps on resolve/unresolve
- [ ] Web UI: collapse resolved roots by default; expand manually; auto-expand on reply post
- [ ] API tests for permissions, idempotency, and discussion status isolation

## Blocked by

- [11-basic-sub-thread-replies.md](./11-basic-sub-thread-replies.md)

## User stories covered

- 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20
- 43, 44, 45 (partial)

## Notes

- Sub-thread resolve notifications deferred to disc-15.
- Collapse state is client-side default; API exposes `isResolved`, `replyCount`, `lastReplyAt` for headers.
- Deep-link hash to root/reply comments should still scroll correctly when collapsed (expand target thread on navigate).
