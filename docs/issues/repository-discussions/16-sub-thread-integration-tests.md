<!-- forge: #201 -->

# Sub-thread integration tests

## Metadata

- ID: disc-16
- Type: AFK
- Status: ready
- Source: docs/prd/discussion-sub-threads.md

## Parent

[PRD: Discussion Sub-Threads (Replies and Per-Comment Resolution)](../../prd/discussion-sub-threads.md)

## What to build

**End-to-end integration tests** for discussion sub-threads across API authorization, nesting, lifecycle, resolution, orphans, and notifications.

Cover the PRD integration scenarios as HTTP-level tests using existing API test patterns (Docker Compose / fixture repository where applicable).

**Scenarios to include:**
- Create root + two replies; GET returns one root with `replies.length === 2` in order.
- Reply-to-reply rejected.
- Reader root author resolves own sub-thread; stranger Reader cannot.
- Writer+ resolves any sub-thread.
- Resolve leaves discussion status unchanged; reply on resolved sub-thread still allowed.
- Reply on Resolved discussion reopens to Open without re-Engage.
- Top-level non-creator comment Engages once; subsequent reply does not Engage.
- Anchored reply persists anchor in nested list (if disc-12 shipped).
- Soft-delete root promotes orphans; reply to deleted root rejected.
- Blocked user cannot post reply (disc-06).
- Sub-thread resolve notifies root author + assignee only, not all subscribers (disc-15).

## Acceptance criteria

- [ ] Integration test: nested list shape and chronological ordering
- [ ] Integration test: reply validation matrix (reply-to-reply, deleted root)
- [ ] Integration test: Engaged vs reply lifecycle on Open and closed discussions
- [ ] Integration test: resolve permissions and discussion status isolation
- [ ] Integration test: orphan promotion after root soft-delete
- [ ] Integration test: blocked user cannot reply
- [ ] Integration test: scoped resolve notifications
- [ ] Tests pass in CI with existing discussion test harness

## Blocked by

- [11-basic-sub-thread-replies.md](./11-basic-sub-thread-replies.md)
- [12-anchored-replies.md](./12-anchored-replies.md)
- [13-sub-thread-resolve-collapse.md](./13-sub-thread-resolve-collapse.md)
- [14-orphan-replies.md](./14-orphan-replies.md)
- [15-sub-thread-resolve-notifications.md](./15-sub-thread-resolve-notifications.md)
- [06-blocked-users-participation-controls.md](./06-blocked-users-participation-controls.md)

## User stories covered

- Cross-cutting verification of stories 1–47 per PRD test matrix

## Notes

- UI collapse/expand is out of scope for automated tests; API fields (`isResolved`, `replyCount`) prove collapse inputs.
- May extend disc-10 harness rather than duplicating fixture setup.
