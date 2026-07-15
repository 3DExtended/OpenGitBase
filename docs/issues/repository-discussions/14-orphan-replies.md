<!-- forge: #199 -->

# Orphan replies after root soft-delete

## Metadata

- ID: disc-14
- Type: AFK
- Status: ready
- Source: docs/prd/discussion-sub-threads.md

## Parent

[PRD: Discussion Sub-Threads (Replies and Per-Comment Resolution)](../../prd/discussion-sub-threads.md)

## What to build

**Orphan reply presentation** when a sub-thread root is soft-deleted but replies remain.

**List projection:**
- Roots with `deletedAt != null` excluded from the top-level root list.
- Replies whose parent root is soft-deleted are **promoted** to the root list for API/UI purposes.
- Promoted entries carry `orphanedFromDeletedRoot: true` (or equivalent flag).
- `parentCommentId` retained in persistence for audit; no data re-parenting.
- Promoted orphans appear in chronological position among roots (by reply `createdAt` or stable ordering rule documented in implementation).

**Reply create guard:**
- POST reply with `parentCommentId` pointing at a soft-deleted root → 400 or 403.
- No new replies on orphan branches (read-only).

**Soft-delete root behavior:**
- Soft-deleting a root does **not** soft-delete its replies.
- Existing edit/soft-delete rules on individual replies unchanged.

**UI:**
- Promoted orphan entries: muted label such as “in reply to deleted comment”.
- No **Reply** button on orphan branches.
- Orphan entries otherwise render reply body (and anchor if present) normally.

## Acceptance criteria

- [ ] Soft-delete root with replies: root hidden from default list
- [ ] Replies to deleted root promoted to root list with `orphanedFromDeletedRoot: true`
- [ ] POST reply to soft-deleted root's id → rejected
- [ ] Replies under deleted root are not cascade soft-deleted
- [ ] Web UI: orphan label; no reply composer on promoted orphans
- [ ] API tests for promotion flag and reply-block on deleted root

## Blocked by

- [11-basic-sub-thread-replies.md](./11-basic-sub-thread-replies.md)

## User stories covered

- 33, 34, 35, 45

## Notes

- Completes reply-create rejection for deleted roots referenced in disc-11 notes.
- Promoted orphans are not resolvable (no root row visible); resolve API on deleted root id → 404 or 400.
